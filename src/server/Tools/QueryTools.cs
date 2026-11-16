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
    private readonly ReleaseUpdateCheck _updates;
    public QueryTools(PluginClient client) : this(client, new ReleaseUpdateCheck()) { }
    internal QueryTools(PluginClient client, ReleaseUpdateCheck updates)
    { _client = client; _updates = updates; }

    [McpServerTool(Name = "nwd_get_document_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Get the active Navisworks document title, path, and model count.")]
    public Task<string> GetDocumentInfo(CancellationToken ct) => Call("get_document_info", new JObject(), ct);

    [McpServerTool(Name = "nwd_get_model_statistics", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Get item count, model count, and current selection count.")]
    public Task<string> GetModelStatistics(CancellationToken ct) => Call("get_model_statistics", new JObject(), ct);

    [McpServerTool(Name = "nwd_get_model_tree", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Get a bounded model tree. max_depth limits levels, max_items caps nodes. output=file writes the full result to a local same-machine file (%LOCALAPPDATA%\\Bimwright\\nwd-mcp\\spill, kept 24 h) and returns its path, schema and a preview.")]
    public Task<string> GetModelTree(int maxDepth = 2, int maxItems = 500, string output = "inline", CancellationToken ct = default)
    {
        var invalid = ResponseBudget.InvalidOutput(output);
        if (invalid != null)
            return Task.FromResult(invalid);
        return Call("get_model_tree", new JObject { ["max_depth"] = maxDepth, ["max_items"] = maxItems }, ct);
    }

    [McpServerTool(Name = "nwd_get_item_properties", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Get property categories for a single item by id.")]
    public Task<string> GetItemProperties(string itemId, CancellationToken ct)
        => Call("get_item_properties", new JObject { ["item_id"] = itemId }, ct);

    [McpServerTool(Name = "nwd_batch_get_properties", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Get properties for many item ids (capped by max_items). output=file writes the full result to a local same-machine file (%LOCALAPPDATA%\\Bimwright\\nwd-mcp\\spill, kept 24 h) and returns its path, schema and a preview.")]
    public Task<string> BatchGetProperties(string[] itemIds, int maxItems = 200, string output = "inline", CancellationToken ct = default)
    {
        var invalid = ResponseBudget.InvalidOutput(output);
        if (invalid != null)
            return Task.FromResult(invalid);
        return Call("batch_get_properties", new JObject { ["item_ids"] = new JArray(itemIds), ["max_items"] = maxItems }, ct);
    }

    [McpServerTool(Name = "nwd_find_items", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Find items with the Navisworks search engine. filtersJson is one filter or an array: {category (default Item), property (default Name), operator equals|contains|startsWith|endsWith (case-insensitive except equals), value}. Returns the top-most matching items only; children of a match are not listed. Use nwd_find_items_by_name to list every item whose name matches.")]
    public Task<string> FindItems(string filtersJson, int maxItems = 500, CancellationToken ct = default)
        => Call("find_items", new JObject { ["filters"] = JToken.Parse(filtersJson), ["max_items"] = maxItems }, ct);

    [McpServerTool(Name = "nwd_find_items_by_name", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Find every item (at any depth) whose display name contains, or with exact=true equals, a string, case-insensitive. output=file writes the full result to a local same-machine file (%LOCALAPPDATA%\\Bimwright\\nwd-mcp\\spill, kept 24 h) and returns its path, schema and a preview.")]
    public Task<string> FindItemsByName(string name, bool exact = false, int maxItems = 500, string output = "inline", CancellationToken ct = default)
    {
        var invalid = ResponseBudget.InvalidOutput(output);
        if (invalid != null)
            return Task.FromResult(invalid);
        return Call("find_items_by_name", new JObject { ["name"] = name, ["exact"] = exact, ["max_items"] = maxItems }, ct);
    }

    [McpServerTool(Name = "nwd_health_check", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true),
     Description("At session start select the intended target, then call nwd_health_check(check_updates=true) once. Routine calls stay offline by default; not_checked/unknown never means up to date. Probes host/document state and loaded server/plugin versions, build ids and alignment. Inspect update.components and report update.notification code/message according to agent_action. UPDATE_CHECK_UNAVAILABLE describes the external lookup_reason, not a local identity fault; no installation authorization is requested. LOCAL_VERSION_AHEAD is informational. Preserve protected_components and explain protected_component_reasons; unobserved_components require connection/runtime observation, not a warning about a known loaded build. Follow update_guidance.mode: only prepare_update, with bundle_update_proposal_allowed=true, contains installation steps and needs installation authorization (reuse existing permission). Other modes contain only check/review steps. No plugin returns server identity with ok:false and host_connected:false. Matching revisions do not verify artifact bytes, compatibility or publisher trust; never downgrade an ahead_of_release component or automatically replace a development build. After reconnect, compare fresh initialize serverInfo and tools/list runtime metadata/schema/annotations with client_verification; the server cannot prove client cache refresh. The host probe uses the UI thread; while Navisworks is busy it answers without it, with host_busy=true and busy_reason (modal_dialog or ui_thread_busy). Read-only; never downloads packages, installs, saves documents or restarts processes.")]
    public async Task<string> HealthCheck(CancellationToken ct = default, bool check_updates = false)
    {
        JObject data;
        var connected = false;
        try
        {
            data = await _client.SendAsync("health_check", new JObject(), ct) as JObject
                ?? throw new NwdGatewayException("API_ERROR", "The plugin returned an invalid health response.");
            connected = true;
        }
        catch (NwdGatewayException ex)
        {
            ct.ThrowIfCancellationRequested();
            data = new JObject { ["ok"] = false, ["error"] = new JObject { ["code"] = ex.Code, ["message"] = ex.Message } };
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.Net.Sockets.SocketException or JsonException)
        {
            ct.ThrowIfCancellationRequested();
            data = new JObject { ["ok"] = false, ["error"] = new JObject
                { ["code"] = "TARGET_UNAVAILABLE", ["message"] = "Health probe failed: " + ex.Message } };
        }
        var gateway = _client.GatewayIdentity;
        var plugin = connected ? data["setup_plugin"] as JObject : null;
        HealthRuntimeInfo.Enrich(data, gateway, connected);
        data["update"] = check_updates
            ? await _updates.CheckAsync(gateway, plugin, ct)
            : ReleaseUpdateCheck.NotChecked(gateway, plugin);
        data["update_guidance"] = HealthRuntimeInfo.UpdateGuidance((JObject)data["update"]!);
        return JsonConvert.SerializeObject(data, Formatting.None);
    }

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
