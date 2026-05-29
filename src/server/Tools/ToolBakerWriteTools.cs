using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Bake;
using Bimwright.Nwd.Server.Handlers;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class ToolBakerWriteTools
{
    private readonly PluginClient _client;
    private readonly NwdMcpConfig _config;

    public ToolBakerWriteTools(PluginClient client, NwdMcpConfig config)
    {
        _client = client;
        _config = config;
    }

    [McpServerTool(Name = "nwd_run_baked_tool"), Description("Execute a registered baked tool by name with parameters.")]
    public async Task<string> RunBakedTool(string name, string paramsJson, CancellationToken ct)
    {
        JObject parsed;
        try
        {
            parsed = string.IsNullOrWhiteSpace(paramsJson) ? new JObject() : JObject.Parse(paramsJson);
        }
        catch (JsonException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = "INVALID_ARGUMENT", message = "params must be a JSON object: " + ex.Message } }, Formatting.Indented);
        }

        BakePaths.EnsureDir(_config);
        using var db = new BakeDb(BakePaths.Db(_config));
        db.Migrate();
        var record = db.GetRegistryRecord(name);
        if (record == null)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = "INVALID_ARGUMENT", message = "baked tool not found: " + name } }, Formatting.Indented);
        }

        try
        {
            var data = await _client.SendAsync("run_baked_tool", new { name, @params = parsed, tool_record = JObject.FromObject(record) }, ct);
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.Indented);
        }
    }

    [McpServerTool(Name = "nwd_accept_bake_suggestion"), Description("Accept a suggested workflow to compile it into a verified baked tool.")]
    public async Task<string> AcceptBakeSuggestion(string suggestionId, string desiredName, CancellationToken ct)
    {
        BakePaths.EnsureDir(_config);
        using var db = new BakeDb(BakePaths.Db(_config));
        db.Migrate();
        return await AcceptBakeSuggestionHandler.HandleAsync(
            db,
            suggestionId,
            desiredName,
            outputChoice: "mcp_only",
            paramsSchema: null,
            pluginApply: async request =>
            {
                var data = await _client.SendAsync("apply_bake", request, ct);
                return data as JObject ?? new JObject();
            });
    }

    [McpServerTool(Name = "nwd_dismiss_bake_suggestion"), Description("Dismiss/snooze an active bake suggestion.")]
    public Task<string> DismissBakeSuggestion(string suggestionId, CancellationToken ct)
    {
        BakePaths.EnsureDir(_config);
        using var db = new BakeDb(BakePaths.Db(_config));
        db.Migrate();
        return Task.FromResult(DismissBakeSuggestionHandler.Handle(db, suggestionId, "snooze_30d"));
    }
}
