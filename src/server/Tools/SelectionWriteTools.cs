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
public sealed class SelectionWriteTools
{
    private readonly PluginClient _client;
    public SelectionWriteTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_clear_selection"), Description("Clear the active Navisworks selection.")]
    public Task<string> ClearSelection(CancellationToken ct) => Call("clear_selection", new JObject(), ct);

    [McpServerTool(Name = "nwd_select_items_by_search"), Description("Select items matching name/property filters.")]
    public Task<string> SelectItemsBySearch(string filtersJson, CancellationToken ct = default)
        => Call("select_items_by_search", new JObject { ["filters"] = JToken.Parse(filtersJson) }, ct);

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
