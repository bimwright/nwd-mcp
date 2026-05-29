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
public sealed class ViewWriteTools
{
    private readonly PluginClient _client;
    public ViewWriteTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_goto_viewpoint"), Description("Move camera to a saved viewpoint id.")]
    public Task<string> GotoViewpoint(string viewpointId, CancellationToken ct)
        => Call("goto_viewpoint", new JObject { ["viewpoint_id"] = viewpointId }, ct);

    [McpServerTool(Name = "nwd_save_viewpoint"), Description("Save the current viewport state under a given name.")]
    public Task<string> SaveViewpoint(string name, CancellationToken ct)
        => Call("save_viewpoint", new JObject { ["name"] = name }, ct);

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
