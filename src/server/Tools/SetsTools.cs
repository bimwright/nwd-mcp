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
public sealed class SetsTools
{
    private readonly PluginClient _client;
    private readonly ServerState _state;

    public SetsTools(PluginClient client, ServerState state)
    {
        _client = client;
        _state = state;
    }

    [McpServerTool(Name = "nwd_list_sets"), Description("List selection and search sets Recurse folders.")]
    public Task<string> ListSets(CancellationToken ct) => Call("list_sets", new JObject(), ct);

    [McpServerTool(Name = "nwd_get_selection_set_items"), Description("Get items belonging to a selection or search set.")]
    public Task<string> GetSelectionSetItems(string setId, CancellationToken ct)
        => Call("get_selection_set_items", new JObject { ["set_id"] = setId }, ct);

    [McpServerTool(Name = "nwd_execute_search_set"), Description("Run a saved search set; optionally select the matches (forced off in read-only mode).")]
    public Task<string> ExecuteSearchSet(string setId, bool select = false, CancellationToken ct = default)
    {
        var p = new JObject { ["set_id"] = setId, ["select"] = _state.ReadOnly ? false : select };
        if (_state.ReadOnly) p["read_only_enforced"] = true;
        return Call("execute_search_set", p, ct);
    }

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
