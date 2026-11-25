using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

public sealed class NwdGatewayException : Exception
{
    public string Code { get; }
    /// <summary>BW-TARGET-001 §6.10 target payload for targeting refusals; null for other errors.</summary>
    public JObject? Target { get; init; }
    public NwdGatewayException(string code, string message) : base(message) => Code = code;
}

public sealed class PluginClient
{
    /// <summary>
    /// clientInfo.name of the MCP client behind this gateway, refreshed by the call filter on every tool call and
    /// sent as <c>client_name</c> with each command. One gateway process serves one client, so a static is enough.
    /// </summary>
    internal static volatile string? ClientName;

    private readonly NwdMcpConfig _config;
    private readonly TargetRegistry _registry;
    private readonly TargetBinding _binding;
    private readonly JObject _gatewayIdentity;
    // The _report reminder points at nwd_report_task_result, which only the meta toolset registers (rvt parity).
    private readonly bool _reportsOn;

    /// <summary>Writes since the last agent report and the _report reminder (FC-RPT-05).</summary>
    internal Reports.ReportTracker Reports { get; } = new();

    /// <summary>Saved agent reports (FC-RPT-04).</summary>
    internal Reports.ReportStore ReportStore { get; }

    public PluginClient(NwdMcpConfig config) : this(config, null, null) { }

    /// <param name="probe">Test seam for process identity (PID + start time).</param>
    internal PluginClient(NwdMcpConfig config, Func<int, ProcessProbe>? probe, Func<int, string?>? windowTitle = null)
    {
        _config = config;
        _registry = new TargetRegistry(config.DescriptorDirectory, probe);
        if (!TargetSelector.TryParse(config.TargetId, out var selector, out var error))
            throw new ArgumentException("Invalid target selector: " + error);
        _binding = new TargetBinding(selector, ListTargets, probe, windowTitle,
            SandboxJson);
        _gatewayIdentity = RuntimeIdentity.Capture(typeof(PluginClient).Assembly, "gateway");
        ReportStore = new Reports.ReportStore(config.ReportDirectory);
        _reportsOn = ToolsetFilter.Resolve(config).Contains("meta");
    }

    /// <summary>The bound target id, or null when nothing is bound. Never selects a target.</summary>
    internal string? BoundTargetId => (string?)_binding.Snapshot()["bound_target_id"];

    internal JObject GatewayIdentity => (JObject)_gatewayIdentity.DeepClone();

    /// <summary>Test seam for FC-TGT-07: TokenIsAppContainer of this process (null = unknown). Defaults to the cached token probe.</summary>
    internal Func<bool?> AppContainerProbe { get; set; } = ClientSandbox.ProcessTokenIsAppContainer;

    /// <summary>Test seam: replaces <see cref="TargetRegistry.List"/> when set (e.g. to simulate AppContainer access denial).</summary>
    internal Func<IReadOnlyList<TargetDescriptor>>? ListTargetsOverride { get; set; }

    /// <summary>FC-TGT-07: whether this gateway runs inside a client AppContainer.</summary>
    internal ClientSandboxResult Sandbox() => ClientSandbox.Detect(AppContainerProbe(), _config.DescriptorDirectory);

    /// <summary>The <c>sandbox</c> object when detected, else null.</summary>
    internal JObject? SandboxJson() => Sandbox() is { Detected: true } s ? s.ToJson(_config.DescriptorDirectory) : null;

    /// <summary>Current binding generation; the call filter pins it when a tool call arrives.</summary>
    internal long Generation => _binding.Generation;

    /// <summary>
    /// Live targets. Inside a client AppContainer (FC-TGT-07), <c>Global\</c> mutex creation or enumerating a
    /// host descriptor folder throws <see cref="UnauthorizedAccessException"/>; treat that as zero targets so
    /// callers report <c>CLIENT_SANDBOXED</c>. Outside a sandbox, access denial still throws.
    /// </summary>
    public IReadOnlyList<TargetDescriptor> ListTargets()
    {
        try { return (ListTargetsOverride ?? _registry.List)(); }
        catch (UnauthorizedAccessException) when (Sandbox().Detected) { return Array.Empty<TargetDescriptor>(); }
    }

    /// <summary>nwd_list_available_targets rows. Never selects a target.</summary>
    public JArray ListTargetRows() => _binding.ListRows();

    /// <summary>nwd_get_current_target state. Never selects a target.</summary>
    public JObject CurrentTargetState() => _binding.Snapshot();

    /// <summary>nwd_switch_target. Throws <see cref="NwdGatewayException"/> with a target payload on refusal.</summary>
    public JObject SwitchTarget(string? selector, bool rebind = false) => _binding.Switch(selector, rebind);

    public const int FileOperationTimeoutMs = 300_000;

    public Task<JToken> SendAsync(string command, object parameters, CancellationToken ct)
        => SendAsync(command, parameters, _config.TimeoutMs, ct);

    public async Task<JToken> SendAsync(string command, object parameters, int timeoutMs, CancellationToken ct)
    {
        var (result, call) = await ExchangeAsync(command, parameters, timeoutMs, ct);
        // Owner decision Q-B: open_file that replaced the confirmed document moves the confirmation to the new file.
        if (result.Ok && result.Meta?.RepinDocumentId is { Length: > 0 } repin)
            _binding.PinDocument(call, repin, result.Meta.DocumentTitle);
        if (!result.Ok) throw PluginFailure(result);
        // FC-RPT-05: a write that changed the document carries _report until the agent reports. A script that
        // failed (ok:false inside a delivered result) changed nothing the agent will report on.
        if (_reportsOn && result.Data is JObject data && !(data["ok"] is JValue { Type: JTokenType.Boolean } ok && !(bool)ok)
            && Reports.Record(command, result.Id.ToString("N"), result.Meta?.DocumentTitle) is { } reminder)
            data["_report"] = reminder;
        return result.Data ?? JValue.CreateNull();
    }

    internal static bool SupportsDocumentTarget(TargetDescriptor target)
        => target.Capabilities?.Contains(DocumentTargetPolicy.Protocol, StringComparer.Ordinal) == true;

    /// <summary>
    /// nwd_switch_target(verify=true): reads the document open in the bound Navisworks now and confirms it for writes.
    /// Runs right after the switch, so it pins the new binding generation rather than the tool call's.
    /// </summary>
    internal async Task<(string Id, string? Title)> ConfirmDocumentAsync(CancellationToken ct)
    {
        using var scope = TargetCallContext.Begin(_binding.Generation);
        var (result, call) = await ExchangeAsync("health_check", new JObject(), _config.TimeoutMs, ct);
        if (!result.Ok) throw PluginFailure(result);
        // FC-BUSY-05: health answered without the UI thread carries no document id; nothing was confirmed.
        if (result.Data is JObject health && health["host_busy"] is JValue { Type: JTokenType.Boolean } busy && (bool)busy)
            throw new NwdGatewayException("HOST_BUSY",
                $"Navisworks is busy ({(string?)health["busy_reason"]}), so the open document could not be read and was not confirmed. " +
                "Ask the user to finish or close what is open in Navisworks, then call nwd_switch_target(\"pid:<n>\", verify=true) again.");
        if (!SupportsDocumentTarget(call.Descriptor) || string.IsNullOrEmpty(result.Meta?.DocumentId))
            throw PairingRequired(call.Descriptor, "verify");
        var id = result.Meta!.DocumentId!;
        if (!_binding.PinDocument(call, id, result.Meta.DocumentTitle))
            throw new NwdGatewayException("TARGET_CHANGED",
                $"The binding changed while {call.Descriptor.TargetId} was being verified; call nwd_switch_target again.");
        return (id, result.Meta.DocumentTitle);
    }

    /// <summary>
    /// Family contract FC-PAIR-01 / FC-DOC-03: a write is not sent to a plug-in without document_target_v1, nor
    /// before a document is confirmed. Returns the confirmed document for the envelope.
    /// </summary>
    private (string Id, string? Title)? GateWrite(string command, TargetCall call)
    {
        var pinned = _binding.PinnedDocument(call);
        // Internal or unknown commands are left to the plug-in's own check.
        if (!NwdCommandCatalog.TryGet(command, out var info) || !DocumentTargetPolicy.NeedsConfirmedTarget(command, info.IsReadOnly))
            return pinned;
        var target = call.Descriptor;
        if (!SupportsDocumentTarget(target))
            throw PairingRequired(target, command);
        if (pinned is null)
            throw NotSent("TARGET_UNCONFIRMED",
                $"{command} was not sent: writes need a confirmed Navisworks and document. Check with the user that " +
                (string.IsNullOrEmpty(target.DocumentTitle) ? $"Navisworks {target.Year} (pid {target.ProcessIdNormalized})" : $"'{target.DocumentTitle}'") +
                $" is what to change, then call nwd_switch_target(\"pid:{target.ProcessIdNormalized}\", verify=true) and repeat the call.",
                target, "nwd_switch_target");
        return pinned;
    }

    private static NwdGatewayException PairingRequired(TargetDescriptor target, string command)
        => NotSent("PAIRING_REQUIRED",
            $"{command} was not sent: the Navisworks plug-in of {target.TargetId} does not confirm documents ({DocumentTargetPolicy.Protocol}). " +
            "Update the nwd-mcp server and the Navisworks plug-in together from the same release, then restart Navisworks and the MCP client. Reads still work.",
            target, "nwd_health_check");

    /// <summary>A failed plug-in result. Document refusals were not started (FC-DOC-04) and carry a target payload.</summary>
    private static NwdGatewayException PluginFailure(NwdCommandResult result)
    {
        var code = result.Error?.Code ?? "API_ERROR";
        var message = result.Error?.Message ?? "unknown error";
        if (code is not ("DOCUMENT_CHANGED" or "DOCUMENT_UNCONFIRMED" or "PAIRING_REQUIRED"))
            return new NwdGatewayException(code, message);
        var ex = new NwdGatewayException(code, message)
        {
            Target = new JObject
            {
                ["code"] = code,
                ["sent"] = true,
                ["outcome"] = "not_started",
                ["document_id"] = result.Meta?.DocumentId,
                ["document_title"] = result.Meta?.DocumentTitle,
                ["recommended_next_tool"] = code == "PAIRING_REQUIRED" ? "nwd_health_check" : "nwd_switch_target",
                ["next_step"] = message
            }
        };
        TargetCallContext.Record(ex);
        return ex;
    }

    private async Task<(NwdCommandResult Result, TargetCall Call)> ExchangeAsync(string command, object parameters, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_config.TimeoutMs <= 0 || timeoutMs <= 0)
            throw new NwdGatewayException("INVALID_ARGUMENT", "timeout must be a positive number of milliseconds");
        if (_config.MaxResponseBytes <= 0)
            throw new NwdGatewayException("INVALID_ARGUMENT", "maxResponseBytes must be positive");
        var wait = Math.Max(timeoutMs, _config.TimeoutMs);
        var context = TargetCallContext.Value;
        var call = Track(() => _binding.Capture(context?.PinnedGeneration));
        var target = call.Descriptor;
        var pinned = GateWrite(command, call);
        var clientName = ClientName;
        var env = new NwdCommandEnvelope
        {
            Id = Guid.NewGuid(),
            Command = command,
            Params = parameters as JObject ?? JObject.FromObject(parameters),
            TimeoutMs = wait,
            AuthToken = target.AuthToken,
            RecordCalls = _config.EnableCallLog,
            DocumentTarget = new DocumentTargetEnvelope { ExpectedDocumentId = pinned?.Id },
            ClientName = string.IsNullOrWhiteSpace(clientName) ? null : clientName
        };
        var serialized = JsonConvert.SerializeObject(env);
        var requestBytes = Encoding.UTF8.GetBytes(serialized + "\n");
        if (requestBytes.Length - 1 > NwdCommandEnvelope.MaxSerializedBytes)
            throw new NwdGatewayException("REQUEST_TOO_LARGE", "Request exceeds 10 MiB. Split large batches or shorten the script before retrying.");

        using var client = new TcpClient();
        try
        {
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectTimeout.CancelAfter(_config.TimeoutMs);
            await client.ConnectAsync("127.0.0.1", target.Port, connectTimeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        { throw NotSent("TIMEOUT", $"connect to target {target.TargetId} timed out; the command was not sent", target); }
        catch (NwdGatewayException) { throw; }
        catch (Exception ex)
        {
            throw NotSent("TARGET_UNAVAILABLE", $"cannot reach target {target.TargetId}: {ex.Message}. The command was not sent; the binding is kept.", target);
        }

        using var stream = client.GetStream();
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestTimeout.CancelAfter(wait);
        Track(() => { _binding.Validate(call); return call; });
        string response;
        try
        {
            await stream.WriteAsync(requestBytes, requestTimeout.Token);
            // Written: a first selection becomes the binding, and this tool call stays pinned to it (§6.4, §6.6).
            _binding.Commit(call);
            if (context is { PinnedGeneration: 0 }) context.PinnedGeneration = _binding.Generation;
            response = await ReadResponse(stream, _config.MaxResponseBytes, requestTimeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            throw new NwdGatewayException("TIMEOUT", $"request {command} timed out after {wait} ms; it may already have run, so do not blindly retry writes");
        }
        catch (Exception ex) when (ex is IOException or SocketException || (ex is NwdGatewayException g && g.Code == "TARGET_UNAVAILABLE"))
        {
            var interrupted = Interrupted(command, target);
            TargetCallContext.Record(interrupted);
            throw interrupted;
        }
        if (!ResponseSizeGuard.Check(response, _config.MaxResponseBytes, out var tooLarge))
            throw new NwdGatewayException(tooLarge!.Code, tooLarge.Message);

        var result = JsonConvert.DeserializeObject<NwdCommandResult>(response)
                     ?? throw new NwdGatewayException("API_ERROR", "unparseable response");
        return (result, call);
    }

    private static TargetCall Track(Func<TargetCall> resolve)
    {
        try { return resolve(); }
        catch (NwdGatewayException ex) { TargetCallContext.Record(ex); throw; }
    }

    /// <summary>Connect failed before any byte was written (BW-TARGET-001 §6.10, <c>sent: false</c>).</summary>
    private static NwdGatewayException NotSent(string code, string message, TargetDescriptor target,
        string recommended = "nwd_get_current_target")
    {
        var ex = new NwdGatewayException(code, message)
        {
            Target = new JObject
            {
                ["code"] = code,
                ["sent"] = false,
                ["outcome"] = "not_started",
                ["previous"] = new JObject { ["target_id"] = target.TargetId, ["pid"] = target.ProcessIdNormalized, ["host_year"] = target.Year },
                ["recommended_next_tool"] = recommended,
                ["next_step"] = message
            }
        };
        TargetCallContext.Record(ex);
        return ex;
    }

    /// <summary>BW-TARGET-001 §6.7: the request was written but the connection closed before a response.</summary>
    private static NwdGatewayException Interrupted(string command, TargetDescriptor target)
    {
        var message = $"The connection to {target.TargetId} closed after {command} was sent and before a response arrived. "
            + "The command may or may not have run; inspect the document state before retrying a write.";
        return new NwdGatewayException("TARGET_INTERRUPTED", message)
        {
            Target = new JObject
            {
                ["code"] = "TARGET_INTERRUPTED",
                ["sent"] = true,
                ["outcome"] = "unknown",
                ["previous"] = new JObject { ["target_id"] = target.TargetId, ["pid"] = target.ProcessIdNormalized, ["host_year"] = target.Year },
                ["recommended_next_tool"] = "nwd_get_document_info",
                ["next_step"] = message
            }
        };
    }

    private static async Task<string> ReadResponse(Stream stream, int maxBytes, CancellationToken ct)
    {
        using var frame = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(), ct);
            if (count == 0) throw new NwdGatewayException("TARGET_UNAVAILABLE", "plug-in closed the connection");
            var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
            var length = newline >= 0 ? newline : count;
            // Permit the optional CR from StreamWriter's Windows newline, but do
            // not allocate an unbounded string before checking response size.
            if (frame.Length + length > (long)maxBytes + 1) throw TooLarge();
            frame.Write(buffer, 0, length);
            if (newline < 0) continue;
            var bytes = frame.ToArray();
            var contentLength = bytes.Length > 0 && bytes[bytes.Length - 1] == '\r' ? bytes.Length - 1 : bytes.Length;
            if (contentLength > maxBytes) throw TooLarge();
            return Encoding.UTF8.GetString(bytes, 0, contentLength);
        }
        NwdGatewayException TooLarge() => new("RESPONSE_TOO_LARGE",
            $"Response exceeds the configured limit of {maxBytes} bytes. A command may already have run; do not blindly retry writes.");
    }
}
