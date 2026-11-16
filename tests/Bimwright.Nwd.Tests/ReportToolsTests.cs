using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Reports;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Tests.Helpers;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>nwd_report_task_result, nwd_get_report_history and the _report reminder (family contract FC-RPT-01..05, plan M3).</summary>
public sealed class ReportToolsTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static FakePlugin PluginAnsweringReports()
    {
        var plugin = new FakePlugin();
        plugin.Reply = env => env.Command == "report_task_result"
            ? NwdCommandResult.Success(env.Id, new JObject
            {
                ["task_id"] = env.Params["task_id"], ["outcome"] = env.Params["outcome"], ["summary"] = env.Params["summary"],
                ["agent_reported"] = true, ["toast_shown"] = true,
            }, new NwdResponseMeta { DocumentId = FakePlugin.DocumentId, DocumentTitle = "fake.nwd" })
            : NwdCommandResult.Success(env.Id, new JObject { ["echo"] = env.Command }, new NwdResponseMeta { DocumentId = FakePlugin.DocumentId, DocumentTitle = "fake.nwd" });
        return plugin;
    }

    [Theory]
    [InlineData("save_viewpoint", true)]
    [InlineData("hide_items", true)]
    [InlineData("import_model", true)]
    [InlineData("send_code", true)]
    [InlineData("open_file", false)]          // replaces the document, does not change it
    [InlineData("goto_viewpoint", false)]     // transient UI state (D11)
    [InlineData("select_items_by_search", false)]
    [InlineData("apply_bake", false)]         // installs a baked tool, no document change
    [InlineData("get_document_info", false)]
    [InlineData("report_task_result", false)]
    public void Only_document_changes_count(string command, bool changes)
        => Assert.Equal(changes, ReportTracker.ChangesDocument(command));

    [Fact]
    public void Report_command_is_read_only_and_runs_off_the_UI_thread()
    {
        Assert.True(NwdCommandCatalog.TryGet("report_task_result", out var info));
        Assert.True(info.IsReadOnly);
        Assert.True(NwdCommandCatalog.RunsOffUiThread("report_task_result"));
        Assert.False(NwdCommandCatalog.RunsOffUiThread("get_document_info"));
    }

    [Fact]
    public void Tracker_reminds_in_full_then_short_and_scripts_once()
    {
        var t = new ReportTracker();
        Assert.Null(t.Record("get_document_info", "r0", "a.nwd"));
        Assert.Equal(ReportTracker.Text, t.Record("save_viewpoint", "r1", "a.nwd"));
        Assert.Equal(ReportTracker.ShortText, t.Record("hide_items", "r2", "a.nwd"));
        Assert.Null(t.Record("send_code", "r3", "a.nwd"));   // a change reminder was already given

        var (calls, truncated) = t.TakeCalls();
        Assert.False(truncated);
        Assert.Equal(new[] { "nwd_save_viewpoint", "nwd_hide_items", "nwd_send_code" }, calls.Select(c => (string?)c["tool"]));
        Assert.True((bool?)calls[2]["unconfirmed_change"]);

        Assert.Equal(ReportTracker.ScriptText, t.Record("send_code", "r4", "a.nwd"));
        Assert.Null(t.Record("run_baked_tool", "r5", "a.nwd"));
        Assert.Equal(ReportTracker.Text, t.Record("save_viewpoint", "r6", "a.nwd"));
    }

    [Fact]
    public void Tracker_keeps_at_most_MaxCalls()
    {
        var t = new ReportTracker();
        for (var i = 0; i <= ReportTracker.MaxCalls; i++) t.Record("hide_items", "r" + i, null);
        var (calls, truncated) = t.TakeCalls();
        Assert.Equal(ReportTracker.MaxCalls, calls.Count);
        Assert.True(truncated);
    }

    [Fact]
    public async Task Write_response_carries_report_until_the_agent_reports()
    {
        using var plugin = PluginAnsweringReports();
        var write = new ViewWriteTools(plugin.Client);
        var read = new QueryTools(plugin.Client);

        Assert.Null(JObject.Parse(await read.GetDocumentInfo(Ct))["_report"]);
        Assert.Equal(ReportTracker.Text, (string?)JObject.Parse(await write.SaveViewpoint("v1", Ct))["_report"]);
        Assert.Equal(ReportTracker.ShortText, (string?)JObject.Parse(await write.SaveViewpoint("v2", Ct))["_report"]);

        var report = JObject.Parse(await new MetaTools(plugin.Client).ReportTaskResult("job-1", "completed", "Saved two viewpoints", "- v1\n- v2", Ct));
        Assert.True((bool?)report["report_saved"]);
        Assert.Equal(2, (int?)report["call_count"]);
        Assert.True((bool?)report["toast_shown"]);
        Assert.True((bool?)report["agent_reported"]);

        Assert.Equal(ReportTracker.Text, (string?)JObject.Parse(await write.SaveViewpoint("v3", Ct))["_report"]);
    }

    [Fact]
    public async Task Failed_script_gets_no_reminder()
    {
        using var plugin = new FakePlugin();
        plugin.Reply = env => NwdCommandResult.Success(env.Id, new JObject { ["ok"] = false, ["error"] = "compile error" },
            new NwdResponseMeta { DocumentId = FakePlugin.DocumentId });
        var text = await new CodeTools(plugin.Client).SendCode("x", ct: Ct);
        Assert.DoesNotContain("_report", text);
        Assert.Empty(plugin.Client.Reports.TakeCalls().Calls);
    }

    [Fact]
    public async Task No_reminder_without_the_meta_toolset()
    {
        using var plugin = PluginAnsweringReports();
        var config = plugin.Config;
        config.Toolsets = new() { "view", "view_write" };
        var client = new PluginClient(config);
        client.SwitchTarget("pid:" + Environment.ProcessId);
        await Task.Run(() => client.ConfirmDocumentAsync(Ct));
        var text = await new ViewWriteTools(client).SaveViewpoint("v1", Ct);
        Assert.DoesNotContain("_report", text);
    }

    [Fact]
    public async Task Report_is_sent_saved_and_read_back()
    {
        using var plugin = PluginAnsweringReports();
        var meta = new MetaTools(plugin.Client);
        await new ViewWriteTools(plugin.Client).SaveViewpoint("v1", Ct);

        var first = JObject.Parse(await meta.ReportTaskResult("job-1", "failed", "One clash left", null, Ct));
        var env = plugin.Received.Last();
        Assert.Equal("report_task_result", env.Command);
        Assert.Equal("job-1", (string?)env.Params["task_id"]);
        Assert.Null(env.Params["body"]);                 // the body stays on this machine's server side
        Assert.False((bool?)first["replaced"]);

        var again = JObject.Parse(await meta.ReportTaskResult("job-1", "completed", "No clashes left", "fixed", Ct));
        Assert.True((bool?)again["replaced"]);
        Assert.Equal((string?)first["report_id"], (string?)again["report_id"]);

        var list = JObject.Parse(meta.GetReportHistory());
        Assert.Equal(1, (int?)list["total"]);
        Assert.Equal("completed", (string?)list["reports"]?[0]?["outcome"]);
        Assert.Equal(1, (int?)list["reports"]?[0]?["call_count"]);   // the resend keeps the first report's writes

        var full = JObject.Parse(meta.GetReportHistory((string?)first["report_id"]));
        Assert.Equal("fixed", (string?)full["body"]);
        Assert.Equal("nwd_save_viewpoint", (string?)full["calls"]?[0]?["tool"]);
        Assert.Equal(plugin.TargetId, (string?)full["source"]?["target_id"]);
    }

    [Theory]
    [InlineData("", "completed", "ok")]
    [InlineData("job", "done", "ok")]
    [InlineData("job", "completed", "two\nlines")]
    [InlineData("job", "completed", "")]
    public async Task Invalid_report_is_refused_without_sending(string taskId, string outcome, string summary)
    {
        using var plugin = PluginAnsweringReports();
        var text = await new MetaTools(plugin.Client).ReportTaskResult(taskId, outcome, summary, null, Ct);
        Assert.Equal("INVALID_ARGUMENT", (string?)JObject.Parse(text)["error"]?["code"]);
        Assert.Empty(plugin.Received);
    }

    [Fact]
    public async Task Report_is_saved_when_Navisworks_is_not_reachable()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwd-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var client = new PluginClient(new NwdMcpConfig { DescriptorDirectory = dir, BakeDirectory = Path.Combine(dir, "baked"), ReportDirectory = Path.Combine(dir, "reports") });
            var meta = new MetaTools(client);
            var result = JObject.Parse(await meta.ReportTaskResult("job-x", "cancelled", "User stopped the job", null, Ct));
            Assert.True((bool?)result["report_saved"]);
            Assert.False((bool?)result["toast_shown"]);
            Assert.NotNull(result["toast_error"]?["code"]);
            Assert.Equal(1, (int?)JObject.Parse(meta.GetReportHistory())["total"]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("xyz", 0, 10)]
    [InlineData(null, -1, 10)]
    [InlineData(null, 0, 51)]
    public void History_rejects_bad_arguments(string? reportId, int offset, int limit)
    {
        using var plugin = new FakePlugin(confirmed: false);
        var text = new MetaTools(plugin.Client).GetReportHistory(reportId, offset, limit);
        Assert.Equal("INVALID_ARGUMENT", (string?)JObject.Parse(text)["error"]?["code"]);
    }
}
