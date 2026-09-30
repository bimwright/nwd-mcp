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

    [McpServerTool(Name = "nwd_hide_items", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false), Description("Hide the listed item ids in the model view, or show them when hide is false. Changes the document's hidden state; call again with the opposite hide value to revert.")]
    public Task<string> HideItems(string[] itemIds, bool hide = true, CancellationToken ct = default)
        => Call("hide_items", new JObject { ["item_ids"] = new JArray(itemIds), ["hide"] = hide }, ct);

    [McpServerTool(Name = "nwd_unhide_all", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false), Description("Show every hidden item in the model view. The previous hidden set is not kept, so restoring it means hiding those item ids again with nwd_hide_items.")]
    public Task<string> UnhideAll(CancellationToken ct) => Call("unhide_all", new JObject(), ct);

    private async Task<string> Call(string command, JObject p, CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync(command, p, ct);
            return JsonConvert.SerializeObject(data, Formatting.None);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.None);
        }
    }
}
