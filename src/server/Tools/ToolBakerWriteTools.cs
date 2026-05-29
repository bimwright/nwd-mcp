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
public sealed class ToolBakerWriteTools
{
    private readonly PluginClient _client;
    public ToolBakerWriteTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_run_baked_tool"), Description("Execute a registered baked tool by name with parameters.")]
    public Task<string> RunBakedTool(string name, string paramsJson, CancellationToken ct)
        => Call("run_baked_tool", new JObject { ["name"] = name, ["params"] = JToken.Parse(paramsJson) }, ct);

    [McpServerTool(Name = "nwd_accept_bake_suggestion"), Description("Accept a suggested workflow to compile it into a verified baked tool.")]
    public Task<string> AcceptBakeSuggestion(string suggestionId, string desiredName, CancellationToken ct)
        => Call("accept_bake_suggestion", new JObject { ["suggestion_id"] = suggestionId, ["desired_name"] = desiredName }, ct);

    [McpServerTool(Name = "nwd_dismiss_bake_suggestion"), Description("Dismiss/snooze an active bake suggestion.")]
    public Task<string> DismissBakeSuggestion(string suggestionId, CancellationToken ct)
        => Call("dismiss_bake_suggestion", new JObject { ["suggestion_id"] = suggestionId }, ct);

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
