using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class FileTools
{
    private readonly PluginClient _client;
    public FileTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_list_recent_files"), Description("List recent files for the running Navisworks Manage year, in File menu order. Each entry has path, display name, pinned, last opened time, and whether the file still exists.")]
    public Task<string> ListRecentFiles(int limit = 25, CancellationToken ct = default)
        => Call("list_recent_files", new JObject { ["limit"] = limit }, ct);

    private async Task<string> Call(string command, JObject p, CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync(command, p, ct);
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.Indented);
        }
    }
}
