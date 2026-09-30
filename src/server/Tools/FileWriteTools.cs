using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class FileWriteTools
{
    private readonly PluginClient _client;
    public FileWriteTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_open_file"), Description("Open a file in the active Navisworks session. Refuses when the current file has unsaved changes unless discard_changes is true. Waits up to 5 minutes.")]
    public Task<string> OpenFile(string path, bool discardChanges = false, CancellationToken ct = default)
        => Call("open_file", new JObject { ["path"] = path, ["discard_changes"] = discardChanges }, ct);

    [McpServerTool(Name = "nwd_import_model"), Description("Bring a model into the open Navisworks document. mode append (default) adds it as its own model. mode merge combines it into the document. Waits up to 5 minutes.")]
    public Task<string> ImportModel(string path, string mode = "append", CancellationToken ct = default)
        => Call("import_model", new JObject { ["path"] = path, ["mode"] = mode }, ct);

    private async Task<string> Call(string command, JObject p, CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync(command, p, PluginClient.FileOperationTimeoutMs, ct);
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.Indented);
        }
    }
}
