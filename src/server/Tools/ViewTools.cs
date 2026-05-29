using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class ViewTools
{
    private readonly PluginClient _client;
    public ViewTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_list_viewpoints"), Description("List saved viewpoints and folders Recurse where possible.")]
    public Task<string> ListViewpoints(CancellationToken ct) => Call("list_viewpoints", new JObject(), ct);

    [McpServerTool(Name = "nwd_get_current_viewpoint"), Description("Get camera and display information for the active viewport.")]
    public Task<string> GetCurrentViewpoint(CancellationToken ct) => Call("get_current_viewpoint", new JObject(), ct);

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
