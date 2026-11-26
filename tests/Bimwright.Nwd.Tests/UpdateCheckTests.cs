using System;
using System.IO;
using System.Threading.Tasks;
using Bimwright.Nwd.Shared.Update;
using Xunit;

namespace Bimwright.Nwd.Tests;

public sealed class UpdateCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-update-test-" + Guid.NewGuid().ToString("N"));
    private string StatePath => Path.Combine(_dir, "update-check.json");

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void ParseLatestRelease_extracts_tag_url_and_formatted_notes()
    {
        var json = """
        {
          "tag_name": "v2.6.0",
          "html_url": "https://github.com/bimwright/nwd-mcp/releases/tag/v2.6.0",
          "draft": false,
          "prerelease": false,
          "body": "### Highlights\n- **Faster** search using `nwd_find_items` ([PR #42](https://github.com/bimwright/nwd-mcp/pull/42))\n\n### Distribution\n- NuGet\n"
        }
        """;

        var parsed = UpdateChecker.ParseLatestRelease(json);
        Assert.NotNull(parsed);
        Assert.Equal("v2.6.0", parsed!.Item1);
        Assert.Equal("https://github.com/bimwright/nwd-mcp/releases/tag/v2.6.0", parsed.Item2);
        Assert.Contains("Highlights", parsed.Item3);
        Assert.Contains("• Faster search using nwd_find_items (PR #42)", parsed.Item3);
        Assert.DoesNotContain("Distribution", parsed.Item3);
        Assert.DoesNotContain("**", parsed.Item3);
        Assert.DoesNotContain("`", parsed.Item3);
    }

    [Fact]
    public void ParseLatestRelease_ignores_prerelease_and_draft()
    {
        Assert.Null(UpdateChecker.ParseLatestRelease("""{"tag_name":"v2.6.0","prerelease":true,"draft":false}"""));
        Assert.Null(UpdateChecker.ParseLatestRelease("""{"tag_name":"v2.6.0","prerelease":false,"draft":true}"""));
        Assert.Null(UpdateChecker.ParseLatestRelease("""{"tag_name":"v2.6.0-rc.1","prerelease":false,"draft":false}"""));
    }

    [Fact]
    public async Task CheckAsync_respects_startup_and_interactive_intervals_and_caches_notes()
    {
        var calls = 0;
        var t0 = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        Task<string?> Fetch(System.Threading.CancellationToken _)
        {
            calls++;
            return Task.FromResult<string?>("""{"tag_name":"v2.6.0","html_url":"https://github.com/bimwright/nwd-mcp/releases/tag/v2.6.0","draft":false,"prerelease":false,"body":"- New clash filter"}""");
        }

        var first = await UpdateChecker.CheckAsync("2.5.2", StatePath, Fetch, t0);
        Assert.NotNull(first);
        Assert.Equal(1, calls);
        Assert.Equal("v2.5.2", first!.CurrentVersion);
        Assert.Equal("v2.6.0", first.LatestVersion);
        Assert.Equal("• New clash filter", first.ReleaseNotes);

        // 20 minutes later: within both 24h and 30m cooldowns -> no extra fetch, cached notes preserved
        var second = await UpdateChecker.CheckAsync("2.5.2", StatePath, Fetch, t0.AddMinutes(20), UpdateChecker.InteractiveCheckInterval);
        Assert.Equal(1, calls);
        Assert.Equal("• New clash filter", second?.ReleaseNotes);

        // 35 minutes later with interactive 30m interval -> fetches again
        var third = await UpdateChecker.CheckAsync("2.5.2", StatePath, Fetch, t0.AddMinutes(35), UpdateChecker.InteractiveCheckInterval);
        Assert.Equal(2, calls);
        Assert.Equal("v2.6.0", third?.LatestVersion);
    }

    [Fact]
    public void SkipVersion_marks_matching_latest_as_skipped()
    {
        var state = new UpdateCheckState { LatestTag = "v2.6.0", LatestUrl = UpdateChecker.ReleasesLatestUrl };
        state.Save(StatePath);

        UpdateChecker.SkipVersion("v2.6.0", StatePath);
        var info = UpdateChecker.FromState("2.5.2", UpdateCheckState.Load(StatePath));

        Assert.NotNull(info);
        Assert.True(info!.IsSkipped);
    }

    [Fact]
    public void BuildAgentPrompt_never_doubles_v_prefix()
    {
        var prompt = UpdateNoticeText.BuildAgentPrompt(new UpdateInfo
        {
            CurrentVersion = "v2.5.2",
            LatestVersion = "v2.6.0",
            ReleaseUrl = "https://github.com/bimwright/nwd-mcp/releases/tag/v2.6.0",
        });

        Assert.Contains("NwdMcp.Setup-v2.6.0-win-x64.zip", prompt);
        Assert.DoesNotContain("vv2.6.0", prompt);
        Assert.Contains("nwd_health_check", prompt);
    }
}
