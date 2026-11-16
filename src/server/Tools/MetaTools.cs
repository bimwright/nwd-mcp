using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class MetaTools
{
    private readonly PluginClient _client;
    public MetaTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_list_available_targets", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("List live Navisworks Manage instances: target_id, host_year, pid, process_start_utc, window_title, document title, and is_current for the bound one. Use pid and window_title to tell instances of the same year apart. Never selects or changes the target.")]
    public string ListAvailableTargets()
        => _client.ListTargetRows().ToString(Formatting.None);

    [McpServerTool(Name = "nwd_get_current_target", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Show the gateway's target binding: binding (none/implicit/explicit), binding_state (none/bound/lost), bound_target_id, bound_pid, window_title, next_target_id, confirmation_required, and document_confirmed / confirmed_document_title for writes. Never selects or changes the target; binding_state none means the next Navisworks command makes the first selection.")]
    public string GetCurrentTarget()
        => _client.CurrentTargetState().ToString(Formatting.None);

    [McpServerTool(Name = "nwd_switch_target", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Pin the Navisworks instance every later command goes to. targetId is a selector: pid:<n> or id:<target_id> from nwd_list_available_targets (explicit), a 4-digit year 2022..2027, or auto. With several instances of one year, pin pid:<n> at session start. After TARGET_LOST or TARGET_CHANGED, inspect the proposed instance and confirm it here with pid:<n>. A year or auto selector never moves an existing binding to another instance (TARGET_REBIND_REQUIRED); leaving a live, explicitly selected instance also needs rebind=true. " +
                 "verify=true also confirms the document open in that Navisworks now (one health check, no change): writes return TARGET_UNCONFIRMED until this is called once, and DOCUMENT_CHANGED (command not started) when another file was opened since. " +
                 "Check with the user that the reported document is the one to change. nwd_open_file moves the confirmation to the file it opens. Selecting the same instance again keeps its confirmed document. Never use legacy R-codes.")]
    public async Task<string> SwitchTarget(
        [Description("pid:<n>, id:<target_id>, a year 2022..2027, or auto.")] string targetId,
        [Description("Required to leave a live instance that was selected explicitly.")] bool rebind = false,
        [Description("Also confirm the open document for writes.")] bool verify = false,
        CancellationToken ct = default)
    {
        try
        {
            var json = _client.SwitchTarget(targetId, rebind);
            if (verify) await _client.ConfirmDocumentAsync(ct);
            var state = _client.CurrentTargetState();
            json["document_confirmed"] = state["document_confirmed"]?.DeepClone();
            json["confirmed_document_id"] = state["confirmed_document_id"]?.DeepClone();
            json["confirmed_document_title"] = state["confirmed_document_title"]?.DeepClone();
            if (state["document_confirmed"]?.Value<bool>() != true)
                json["document_next_step"] = $"Before writes: nwd_switch_target(\"pid:{state["bound_pid"]}\", verify=true).";
            return json.ToString(Formatting.None);
        }
        catch (NwdGatewayException ex)
        {
            return new JObject
            {
                ["ok"] = false,
                ["error"] = new JObject { ["code"] = ex.Code, ["message"] = ex.Message, ["target"] = ex.Target }
            }.ToString(Formatting.None);
        }
    }
}
