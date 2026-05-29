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
public sealed class VisibilityWriteTools
{
    private readonly PluginClient _client;
    public VisibilityWriteTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_hide_items"), Description("Hide specified item ids in the model view.")]
    public Task<string> HideItems(string[] itemIds, bool hide = true, CancellationToken ct = default)
        => Call("hide_items", new JObject { ["item_ids"] = new JArray(itemIds), ["hide"] = hide }, ct);

    [McpServerTool(Name = "nwd_unhide_all"), Description("Unhide/show all items in the model view.")]
    public Task<string> UnhideAll(CancellationToken ct) => Call("unhide_all", new JObject(), ct);

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
