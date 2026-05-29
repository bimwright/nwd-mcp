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
public sealed class SelectionTools
{
    private readonly PluginClient _client;
    public SelectionTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_get_current_selection"), Description("Get a list of currently selected model item ids.")]
    public async Task<string> GetCurrentSelection(CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync("get_current_selection", new JObject(), ct);
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.Indented);
        }
    }
}
