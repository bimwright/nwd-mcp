using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
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
    {
        var rows = _client.ListTargetRows();
        // FC-TGT-07: an empty list inside a client AppContainer says why instead of looking like "Navisworks is closed".
        if (rows.Count == 0 && _client.SandboxJson() is { } sandbox)
            return new JObject { ["targets"] = rows, ["sandbox"] = sandbox, ["note"] = ClientSandbox.Message }.ToString(Formatting.None);
        return rows.ToString(Formatting.None);
    }

    [McpServerTool(Name = "nwd_get_current_target", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Show the gateway's target binding: binding (none/implicit/explicit), binding_state (none/bound/lost), bound_target_id, bound_pid, window_title, next_target_id, confirmation_required, and document_confirmed / confirmed_document_title for writes. Never selects or changes the target; binding_state none means the next Navisworks command makes the first selection.")]
    public string GetCurrentTarget()
        => _client.CurrentTargetState().ToString(Formatting.None);

    [McpServerTool(Name = "nwd_report_task_result", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Report an agent task's final outcome to the user: a summary card on the Navisworks toast and a report saved under %LOCALAPPDATA%\\Bimwright\\nwd-mcp\\reports (read back with nwd_get_report_history). " +
                 "Call once after verifying the whole requested job, when it changed a document or found something the user should check (clashes, missing properties); never after each tool, merely because tools went idle, or for a chat-only answer. Responses of document writes carry _report until you do. " +
                 "task_id must be unique per agent/job (1-80 chars; resending it from this session replaces that report); outcome is completed, failed or cancelled; summary is a truthful single-line result (1-120 chars); body is optional Markdown detail, max 32 KiB UTF-8. " +
                 "The report records the write tools this session ran since the last report. Saved even when Navisworks is not reachable (toast_shown=false). Agent-reported, not independently verified. Does not change the document.")]
    public async Task<string> ReportTaskResult(string task_id, string outcome, string summary,
        [Description("Optional Markdown detail (what changed, what to check), max 32 KiB UTF-8.")] string? body = null,
        CancellationToken ct = default)
    {
        var invalid = ReportLimits.Validate(task_id, outcome, summary, body);
        if (invalid != null) return Error("INVALID_ARGUMENT", invalid);

        JObject? card = null;
        NwdGatewayException? cardError = null;
        try { card = await _client.SendAsync("report_task_result", new JObject { ["task_id"] = task_id, ["outcome"] = outcome, ["summary"] = summary }, ct) as JObject; }
        catch (NwdGatewayException ex) { cardError = ex; }

        var (calls, truncated) = _client.Reports.TakeCalls();
        var report = new JObject
        {
            ["schema"] = 1,
            ["task_id"] = task_id.Trim(), ["outcome"] = outcome.Trim(), ["summary"] = summary.Trim(), ["body"] = body,
            ["agent_reported"] = true,
            ["source"] = new JObject { ["server_pid"] = Environment.ProcessId, ["target_id"] = _client.BoundTargetId },
            ["calls"] = calls, ["calls_truncated"] = truncated,
        };
        string? saveError = null;
        (string Id, bool Replaced, string Path) saved = default;
        try { saved = _client.ReportStore.Save(report); }
        catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException) { saveError = ex.Message; }

        if (saveError != null && cardError != null)
            return Error(cardError.Code, cardError.Message + " The report was not saved either: " + saveError);
        var result = card ?? new JObject { ["task_id"] = task_id.Trim(), ["outcome"] = outcome.Trim(), ["summary"] = summary.Trim(), ["agent_reported"] = true };
        result["report_saved"] = saveError == null;
        if (saveError == null)
        {
            result["report_id"] = saved.Id;
            result["replaced"] = saved.Replaced;
            result["call_count"] = (report["calls"] as JArray)?.Count ?? 0;
        }
        else result["save_error"] = saveError;
        if (cardError != null)
        {
            result["toast_shown"] = false;
            result["toast_error"] = new JObject { ["code"] = cardError.Code, ["message"] = cardError.Message };
        }
        return result.ToString(Formatting.None);
    }

    [McpServerTool(Name = "nwd_get_report_history", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Read saved agent reports (nwd_report_task_result) from %LOCALAPPDATA%\\Bimwright\\nwd-mcp\\reports, newest first. Without report_id: a page of summaries (offset >= 0, limit 1-50, default 10) and the total. " +
                 "With report_id (32 hex characters): that report in full, with its body and the write tools recorded since the report before it. Read-only local data; no Navisworks connection required. Example: nwd_get_report_history(limit=5).")]
    public string GetReportHistory(string? report_id = null, int offset = 0, int limit = 10)
    {
        if (!string.IsNullOrEmpty(report_id))
        {
            if (!Reports.ReportStore.IsReportId(report_id))
                return Error("INVALID_ARGUMENT", "report_id must be 32 hexadecimal characters.");
            return _client.ReportStore.Get(report_id) is { } report
                ? report.ToString(Formatting.None)
                : Error("INVALID_ARGUMENT", "No saved report has this report_id (the newest 500 are kept).");
        }
        if (offset < 0 || limit is < 1 or > 50)
            return Error("INVALID_ARGUMENT", "offset must be >= 0 and limit 1-50.");
        var (items, total) = _client.ReportStore.List(offset, limit);
        return new JObject { ["total"] = total, ["offset"] = offset, ["reports"] = items, ["agent_reported"] = true }.ToString(Formatting.None);
    }

    private static string Error(string code, string message)
        => new JObject { ["ok"] = false, ["error"] = new JObject { ["code"] = code, ["message"] = message } }.ToString(Formatting.None);

    [McpServerTool(Name = "nwd_switch_target", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Pin the Navisworks instance every later command goes to; the binding (PID + process start) never moves on its own. targetId is a selector: pid:<n> or id:<target_id> from nwd_list_available_targets (explicit), a 4-digit year 2022..2027, or auto. With several instances of one year, pin pid:<n> at session start. AMBIGUOUS_TARGET, TARGET_LOST, TARGET_CHANGED, TARGET_UNAVAILABLE and NO_TARGET from any tool mean the command was not sent: inspect error.target.candidates, confirm the instance here with pid:<n>, then call again. A year or auto selector never moves an existing binding to another instance (TARGET_REBIND_REQUIRED); leaving a live, explicitly selected instance also needs rebind=true. " +
                 "verify=true also confirms the document open in that Navisworks now (one health check, no change): writes return TARGET_UNCONFIRMED (or PAIRING_REQUIRED; both not sent) until this is called once, and DOCUMENT_CHANGED (command not started) when another file was opened since: ask the user, then verify again. " +
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
