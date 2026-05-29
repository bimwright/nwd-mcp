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
public sealed class QueryTools
{
    private readonly PluginClient _client;
    public QueryTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_get_document_info"), Description("Get the active Navisworks document title, path, and model count.")]
    public Task<string> GetDocumentInfo(CancellationToken ct) => Call("get_document_info", new JObject(), ct);

    [McpServerTool(Name = "nwd_get_model_statistics"), Description("Get item count, model count, and current selection count.")]
    public Task<string> GetModelStatistics(CancellationToken ct) => Call("get_model_statistics", new JObject(), ct);

    [McpServerTool(Name = "nwd_get_model_tree"), Description("Get a bounded model tree. max_depth limits levels, max_items caps nodes.")]
    public Task<string> GetModelTree(int maxDepth = 2, int maxItems = 500, CancellationToken ct = default)
        => Call("get_model_tree", new JObject { ["max_depth"] = maxDepth, ["max_items"] = maxItems }, ct);

    [McpServerTool(Name = "nwd_get_item_properties"), Description("Get property categories for a single item by id.")]
    public Task<string> GetItemProperties(string itemId, CancellationToken ct)
        => Call("get_item_properties", new JObject { ["item_id"] = itemId }, ct);

    [McpServerTool(Name = "nwd_batch_get_properties"), Description("Get properties for many item ids (capped by max_items).")]
    public Task<string> BatchGetProperties(string[] itemIds, int maxItems = 200, CancellationToken ct = default)
        => Call("batch_get_properties", new JObject { ["item_ids"] = new JArray(itemIds), ["max_items"] = maxItems }, ct);

    [McpServerTool(Name = "nwd_find_items"), Description("Find items by property/category/name filters.")]
    public Task<string> FindItems(string filtersJson, int maxItems = 500, CancellationToken ct = default)
        => Call("find_items", new JObject { ["filters"] = JToken.Parse(filtersJson), ["max_items"] = maxItems }, ct);

    [McpServerTool(Name = "nwd_find_items_by_name"), Description("Find items whose display name contains or equals a string.")]
    public Task<string> FindItemsByName(string name, bool exact = false, int maxItems = 500, CancellationToken ct = default)
        => Call("find_items_by_name", new JObject { ["name"] = name, ["exact"] = exact, ["max_items"] = maxItems }, ct);

    [McpServerTool(Name = "nwd_health_check"), Description("Ping the current Navisworks plug-in: year, process id, document state.")]
    public Task<string> HealthCheck(CancellationToken ct) => Call("health_check", new JObject(), ct);

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
