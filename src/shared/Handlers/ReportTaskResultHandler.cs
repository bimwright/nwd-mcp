#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Handlers;

/// <summary>
/// Explicit agent-reported outcome (family contract FC-RPT-01..03); does not infer completion or touch the document.
/// The toast turns the returned DTO into a summary card. Runs on the transport thread (no Navisworks API), so the
/// report arrives while Navisworks is busy.
/// </summary>
public sealed class ReportTaskResultHandler : INwdCommand
{
    public string Name => "report_task_result";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var taskId = ReportLimits.SingleLine(p["task_id"], ReportLimits.TaskIdLength);
        var summary = ReportLimits.SingleLine(p["summary"], ReportLimits.SummaryLength);
        var outcome = ReportLimits.SingleLine(p["outcome"], 20);
        if (taskId == null || summary == null || (outcome != "completed" && outcome != "failed" && outcome != "cancelled"))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT",
                "task_id (1-80 chars) and summary (1-120 chars) must be nonempty single-line strings; outcome must be completed, failed or cancelled.", meta);
        return NwdCommandResult.Success(System.Guid.Empty, new JObject
        {
            ["task_id"] = taskId, ["outcome"] = outcome, ["summary"] = summary,
            ["agent_reported"] = true,
            // The card is queued when the toast is on; with the toast off the report is only saved and listed in History.
            ["toast_shown"] = NwdActivityToast.Enabled,
        }, meta);
    }
}
#endif
