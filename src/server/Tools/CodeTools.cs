using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class CodeTools
{
    private const string Legacy = "legacy";
    private const string TaskV1 = "task_v1";
    private readonly PluginClient _client;
    public CodeTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_send_code"), Description(
        "Run a C# script inside Navisworks on its UI thread. doc is the active Document; the value of the last expression comes back as result and Console output as stdout. " +
        "Imported: System, System.Collections.Generic, System.Linq, Autodesk.Navisworks.Api (and Bimwright.Nwd.Scripting with task_v1). " +
        "Batch edits in using (var t = doc.BeginTransaction(\"name\")) { ...; t.Commit(); }: Navisworks requires Commit after the edits and cannot roll a transaction back, so edits made before an exception stay. " +
        "A compile or runtime error returns ok=false with the first 20 diagnostics, or the cause's type and message, masked and without a stack trace. Return warnings and skipped items in the result. " +
        "Put values that change from run to run (names, ids, lists, numbers) in data, not in the code: the script reads them as data[\"key\"] (Newtonsoft JToken). " +
        "result_contract=\"task_v1\" (default \"legacy\"): the script must return McpTaskResult.Pass(value) or McpTaskResult.Fail(code, message, details?) (imported automatically); " +
        "the response carries _task{contract, status: passed|failed|not_reported, error{code, message}}, Fail sets ok=false, and no McpTaskResult fails with TASK_RESULT_REQUIRED. " +
        "Fail does not undo changes the script already made. Loaded DLLs whose metadata cannot be read are left out of the compile and listed in skipped_references. " +
        "Navisworks is blocked while the script runs, so keep it short and do not await. Output above 1 MiB auto-spills to a local same-machine file with schema and preview; there is no output parameter.")]
    public Task<string> SendCode(
        string code,
        [Description("JSON input; the script sees it as the variable `data` (JToken). Omit when the script needs none.")] JsonElement? data = null,
        [Description("\"legacy\" (default) or \"task_v1\": return McpTaskResult.Pass/Fail and read _task.")] string result_contract = Legacy,
        CancellationToken ct = default)
    {
        if (result_contract is not (Legacy or TaskV1))
            return Task.FromResult(Error("INVALID_ARGUMENT", "result_contract must be \"legacy\" or \"task_v1\""));
        if (!TryData(data, out var dataJson, out var dataError))
            return Task.FromResult(Error("INVALID_ARGUMENT", dataError!));
        var p = new JObject { ["code"] = code };
        // Only sent when set, so a plain request keeps its wire shape for older plug-ins.
        if (dataJson != null) p["data_json"] = dataJson;
        if (result_contract == TaskV1) p["result_contract"] = TaskV1;
        return Call("send_code", p, ct);
    }

    /// <summary>
    /// send_code <c>data</c>, read the way the plug-in prelude reads <c>data_json</c>: one JSON value, no duplicate keys,
    /// dates stay strings, other numbers must be finite doubles. Its compact text travels as <c>data_json</c>.
    /// </summary>
    internal static bool TryData(JsonElement? data, out string? json, out string? error)
    {
        json = null;
        error = null;
        if (data is not { } element || element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return true;
        try
        {
            using var reader = new JsonTextReader(new StringReader(element.GetRawText()))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double
            };
            var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var bad = ((token as JContainer)?.DescendantsAndSelf() ?? new[] { token }).OfType<JValue>()
                .FirstOrDefault(v => v.Value is double d && (double.IsInfinity(d) || double.IsNaN(d)));
            if (bad != null)
            {
                error = $"data number at '{bad.Path}' is not a finite double; send it as a string";
                return false;
            }
            json = token.ToString(Formatting.None);
            return true;
        }
        catch (JsonReaderException ex)
        {
            error = "data is not valid JSON for the script: " + ex.Message;
            return false;
        }
    }

    private static string Error(string code, string message)
        => JsonConvert.SerializeObject(new { ok = false, error = new { code, message } }, Formatting.None);

    private async Task<string> Call(string command, JObject p, CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync(command, p, ct);
            return JsonConvert.SerializeObject(data, Formatting.None);
        }
        catch (NwdGatewayException ex)
        {
            return Error(ex.Code, ex.Message);
        }
    }
}
