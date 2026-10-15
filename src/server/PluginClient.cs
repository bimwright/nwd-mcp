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
    public NwdGatewayException(string code, string message) : base(message) => Code = code;
}

public sealed class PluginClient
{
    private readonly NwdMcpConfig _config;
    private readonly TargetRegistry _registry;
    private readonly JObject _gatewayIdentity;
    private TargetDescriptor? _current;

    public PluginClient(NwdMcpConfig config)
    {
        _config = config;
        _registry = new TargetRegistry(config.DescriptorDirectory);
        _gatewayIdentity = RuntimeIdentity.Capture(typeof(PluginClient).Assembly, "gateway");
    }

    internal JObject GatewayIdentity => (JObject)_gatewayIdentity.DeepClone();

    public IReadOnlyList<TargetDescriptor> ListTargets() => _registry.List();

    public TargetDescriptor? CurrentTarget
    {
        get
        {
            var live = _registry.List();
            var refreshed = _current is not null ? live.FirstOrDefault(t => t.TargetId == _current.TargetId) : null;
            if (refreshed is not null) return _current = refreshed;
            _current = (_config.TargetId is { } id ? live.FirstOrDefault(t => t.TargetId == id) : null) ?? live.FirstOrDefault();
            return _current;
        }
    }

    public bool SwitchTarget(string targetId)
    {
        var match = _registry.List().FirstOrDefault(t => t.TargetId == targetId);
        if (match is null) return false;
        _current = match;
        return true;
    }

    public const int FileOperationTimeoutMs = 300_000;

    public Task<JToken> SendAsync(string command, object parameters, CancellationToken ct)
        => SendAsync(command, parameters, _config.TimeoutMs, ct);

    public async Task<JToken> SendAsync(string command, object parameters, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_config.TimeoutMs <= 0 || timeoutMs <= 0)
            throw new NwdGatewayException("INVALID_ARGUMENT", "timeout must be a positive number of milliseconds");
        if (_config.MaxResponseBytes <= 0)
            throw new NwdGatewayException("INVALID_ARGUMENT", "maxResponseBytes must be positive");
        var wait = Math.Max(timeoutMs, _config.TimeoutMs);
        var target = CurrentTarget ?? throw new NwdGatewayException("NO_TARGET", "No live Navisworks Manage target. Start Navisworks with the nwd plug-in loaded.");
        var env = new NwdCommandEnvelope
        {
            Id = Guid.NewGuid(),
            Command = command,
            Params = parameters as JObject ?? JObject.FromObject(parameters),
            TimeoutMs = wait,
            AuthToken = target.AuthToken,
            RecordCalls = _config.EnableCallLog
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
        { throw new NwdGatewayException("TIMEOUT", $"connect to target {target.TargetId} timed out"); }
        catch (NwdGatewayException) { throw; }
        catch (Exception ex)
        {
            throw new NwdGatewayException("TARGET_UNAVAILABLE", $"cannot reach target {target.TargetId}: {ex.Message}");
        }

        using var stream = client.GetStream();
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestTimeout.CancelAfter(wait);
        string response;
        try
        {
            await stream.WriteAsync(requestBytes, requestTimeout.Token);
            response = await ReadResponse(stream, _config.MaxResponseBytes, requestTimeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            throw new NwdGatewayException("TIMEOUT", $"request {command} timed out after {wait} ms; it may already have run, so do not blindly retry writes");
        }
        if (!ResponseSizeGuard.Check(response, _config.MaxResponseBytes, out var tooLarge))
            throw new NwdGatewayException(tooLarge!.Code, tooLarge.Message);

        var result = JsonConvert.DeserializeObject<NwdCommandResult>(response)
                     ?? throw new NwdGatewayException("API_ERROR", "unparseable response");
        if (!result.Ok) throw new NwdGatewayException(result.Error?.Code ?? "API_ERROR", result.Error?.Message ?? "unknown error");
        return result.Data ?? JValue.CreateNull();
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
