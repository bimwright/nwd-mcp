using System.Diagnostics;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Reports;
using Bimwright.Nwd.Shared.Logging;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>BIMWRIGHT_KEEP_ALL_LOGS: every test pins the switch through the test hook, never the real environment.</summary>
[Collection("Call log")]
public sealed class KeepAllLogsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-keepall-" + Guid.NewGuid().ToString("N"));
    private string Active => Path.Combine(_dir, NwdCallLog.FileName);

    public KeepAllLogsTests() => Directory.CreateDirectory(_dir);

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("Yes", true)]
    [InlineData("on", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("off", false)]
    [InlineData("2", false)]
    [InlineData("enabled", false)]
    public void Variable_is_on_only_for_1_true_yes_on_case_insensitive_and_trimmed(string? raw, bool expected)
        => Assert.Equal(expected, LogRetentionPolicy.IsOn(raw));

    [Fact]
    public void Override_pins_keep_all_on_and_off()
    {
        LogRetentionPolicy.Override = true;
        Assert.True(LogRetentionPolicy.KeepAll);
        LogRetentionPolicy.Override = false;
        Assert.False(LogRetentionPolicy.KeepAll);
    }

    [Fact]
    public void Keep_all_on_append_keeps_old_and_over_budget_archives()
    {
        LogRetentionPolicy.Override = true;
        var now = DateTime.UtcNow;
        for (var i = 0; i < 8; i++) BoundedLogFile.Append(Active, new string('x', 90), 128, 256, now);
        var archive = BoundedLogFile.Files(_dir, "mcp-calls").First(f => f != Active);
        File.SetLastWriteTimeUtc(archive, now.AddDays(-31));
        BoundedLogFile.Append(Active, "latest", 128, 256, now);
        Assert.True(File.Exists(archive));
        var files = BoundedLogFile.Files(_dir, "mcp-calls");
        Assert.True(files.Sum(f => new FileInfo(f).Length) > 256);
        Assert.Equal(9, files.SelectMany(File.ReadLines).Count());
    }

    [Fact]
    public void Keep_all_off_append_prunes_old_and_over_budget_archives()
    {
        LogRetentionPolicy.Override = false;
        var now = DateTime.UtcNow;
        for (var i = 0; i < 8; i++) BoundedLogFile.Append(Active, new string('x', 90), 128, 256, now);
        Assert.True(BoundedLogFile.Files(_dir, "mcp-calls").Sum(f => new FileInfo(f).Length) <= 256);
        var archive = BoundedLogFile.Files(_dir, "mcp-calls").First(f => f != Active);
        File.SetLastWriteTimeUtc(archive, now.AddDays(-31));
        BoundedLogFile.Append(Active, "latest", 128, 256, now);
        Assert.False(File.Exists(archive));
    }

    [Fact]
    public void Keep_all_on_rotation_still_archives_to_unique_timestamped_files_without_overwriting()
    {
        LogRetentionPolicy.Override = true;
        var now = DateTime.UtcNow;
        for (var i = 0; i < 20; i++)
            BoundedLogFile.Append(Active, new JObject { ["id"] = i, ["text"] = new string('y', 60) }.ToString(Newtonsoft.Json.Formatting.None), 128, 256, now);
        var files = BoundedLogFile.Files(_dir, "mcp-calls");
        Assert.True(files.Length > 2);
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, 128));
        Assert.All(files.Where(f => f != Active), f => Assert.Matches(@"^mcp-calls-\d{8}-\d{13}-[0-9a-f]{32}\.jsonl$", Path.GetFileName(f)));
        var ids = files.SelectMany(File.ReadLines).Select(l => (int)JObject.Parse(l)["id"]!).OrderBy(i => i);
        Assert.Equal(Enumerable.Range(0, 20), ids);
    }

    [Fact]
    public void Keep_all_on_locked_old_archive_does_not_abort_append()
    {
        LogRetentionPolicy.Override = true;
        var now = DateTime.UtcNow;
        BoundedLogFile.Append(Active, new string('x', 90), 128, 256, now);
        BoundedLogFile.Append(Active, new string('y', 90), 128, 256, now);
        var archive = BoundedLogFile.Files(_dir, "mcp-calls").Single(f => f != Active);
        File.SetLastWriteTimeUtc(archive, now.AddDays(-31));
        using (new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read))
            BoundedLogFile.Append(Active, "written", 128, 256, now);
        Assert.Contains("written", File.ReadAllText(Active));
        Assert.True(File.Exists(archive));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Startup_maintenance_keeps_old_archives_only_when_keep_all_is_on(bool keepAll)
    {
        LogRetentionPolicy.Override = keepAll;
        NwdCallLog.DirectoryOverride = _dir;
        var old = SeedOldArchive(_dir, "mcp-calls");
        using (var stream = File.Create(Active)) stream.SetLength(BoundedLogFile.FileBytes + 1);
        NwdCallLog.Maintain();
        Assert.False(File.Exists(Active)); // Rotation happens either way.
        Assert.Equal(keepAll, File.Exists(old));
        Assert.Equal(keepAll ? 2 : 1, BoundedLogFile.Files(_dir, "mcp-calls").Length);
    }

    [Theory]
    [InlineData(true, ReportStore.Keep + 2)]
    [InlineData(false, ReportStore.Keep)]
    public void Report_cap_deletes_nothing_when_keep_all_is_on(bool keepAll, int expectedFiles)
    {
        LogRetentionPolicy.Override = keepAll;
        var reports = Path.Combine(_dir, "reports");
        Directory.CreateDirectory(reports);
        for (var i = 0; i <= ReportStore.Keep; i++)
            File.WriteAllText(Path.Combine(reports, "20200101T000000" + i.ToString("D3") + "Z-" + Guid.NewGuid().ToString("N") + ".json"), "{}");
        new ReportStore(reports).Save(new JObject { ["task_id"] = "t1", ["outcome"] = "pass", ["summary"] = "s" });
        Assert.Equal(expectedFiles, Directory.GetFiles(reports, "*.json").Length);
    }

    [Fact]
    public async Task Actual_gateway_startup_with_variable_set_keeps_old_archives_and_logs_one_stderr_line()
    {
        var data = Path.Combine(_dir, "Bimwright", "nwd-mcp");
        Directory.CreateDirectory(data);
        var old = SeedOldArchive(data, "nwd-mcp-calls");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(PluginClient).Assembly.Location);
        start.ArgumentList.Add("--disable-call-log");
        start.Environment["LOCALAPPDATA"] = _dir;
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("BIMWRIGHT_NWD_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(key);
        start.Environment[LogRetentionPolicy.EnvironmentVariable] = "on";
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        try
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var err = await stderr;
            Assert.True(process.ExitCode == 0, err);
            Assert.True(File.Exists(old));
            Assert.Single(err.Split('\n'), l => l.Contains(LogRetentionPolicy.StartupNotice, StringComparison.Ordinal));
            Assert.DoesNotContain("KEEP_ALL", await stdout);
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    private static string SeedOldArchive(string directory, string group)
    {
        var path = Path.Combine(directory, group + "-20200101-0000000000000-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllText(path, "{\"old\":true}\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-400));
        return path;
    }

    public void Dispose()
    {
        LogRetentionPolicy.Override = null;
        NwdCallLog.DirectoryOverride = null;
        NwdCallLog.SetEnabled(false);
        Directory.Delete(_dir, true);
    }
}
