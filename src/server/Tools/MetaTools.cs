using System;
using System.ComponentModel;
using System.Linq;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class MetaTools
{
    private readonly PluginClient _client;
    public MetaTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_list_available_targets"), Description("List live Navisworks Manage instances (target id, year, document).")]
    public string ListAvailableTargets()
        => JsonConvert.SerializeObject(_client.ListTargets().Select(t => new { t.TargetId, t.NavisworksYear, t.DocumentTitle }), Formatting.Indented);

    [McpServerTool(Name = "nwd_get_current_target"), Description("Show which Navisworks instance the gateway is currently pointed at.")]
    public string GetCurrentTarget()
    {
        var t = _client.CurrentTarget;
        return t is null
            ? JsonConvert.SerializeObject(new { ok = false, error = new { code = "NO_TARGET", message = "no live target" } }, Formatting.Indented)
            : JsonConvert.SerializeObject(new { t.TargetId, t.NavisworksYear, t.DocumentTitle }, Formatting.Indented);
    }

    [McpServerTool(Name = "nwd_switch_target"), Description("Point the gateway at a specific target id from nwd_list_available_targets. Use 4-digit years (2022..2027), never R-codes.")]
    public string SwitchTarget(string targetId)
        => JsonConvert.SerializeObject(new { ok = _client.SwitchTarget(targetId), target_id = targetId }, Formatting.Indented);
}
