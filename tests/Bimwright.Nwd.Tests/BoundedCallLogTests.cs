using System.Text;
using System.Diagnostics;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Logging;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

[Collection("Call log")]
public sealed class BoundedCallLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-bounded-" + Guid.NewGuid().ToString("N"));
    private string Active => Path.Combine(_dir, NwdCallLog.FileName);
    public BoundedCallLogTests()
    {
        Directory.CreateDirectory(_dir);
        LogRetentionPolicy.Override = false; // These tests check pruning; never depend on BIMWRIGHT_KEEP_ALL_LOGS.
    }

    [Fact]
    public void Rotation_keeps_complete_records_in_unique_bounded_archives()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 20; i++)
            BoundedLogFile.Append(Active, new JObject { ["id"] = i, ["text"] = new string('é', 20) }.ToString(Newtonsoft.Json.Formatting.None), 128, 4096, now);
        var files = BoundedLogFile.Files(_dir, "mcp-calls");
        Assert.True(files.Length > 1);
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, 128));
        var rows = files.SelectMany(File.ReadLines).Select(JObject.Parse).ToArray();
        Assert.Equal(20, rows.Length);
        Assert.Equal(20, rows.Select(r => (int)r["id"]!).Distinct().Count());
    }

    [Fact]
    public void Group_budget_and_age_prune_only_known_archives()
    {
        var now = DateTime.UtcNow;
        var unrelated = Path.Combine(_dir, "mcp-calls-owner-notes.jsonl");
        File.WriteAllText(unrelated, "keep");
        for (var i = 0; i < 8; i++) BoundedLogFile.Append(Active, new string('x', 90), 128, 256, now);
        Assert.True(BoundedLogFile.Files(_dir, "mcp-calls").Sum(f => new FileInfo(f).Length) <= 256);
        var archive = BoundedLogFile.Files(_dir, "mcp-calls").First(f => f != Active);
        File.SetLastWriteTimeUtc(archive, now.AddDays(-31));
        BoundedLogFile.Append(Active, "latest", 128, 256, now);
        Assert.False(File.Exists(archive));
        Assert.Equal("keep", File.ReadAllText(unrelated));
    }

    [Fact]
    public void Giant_record_and_unknown_name_never_modify_files()
    {
        File.WriteAllText(Active, "original\n");
        Assert.Throws<InvalidDataException>(() => BoundedLogFile.Append(Active, new string('x', 129), 128, 256, DateTime.UtcNow));
        Assert.Throws<InvalidDataException>(() => BoundedLogFile.Append(Path.Combine(_dir, "owner.jsonl"), "no"));
        Assert.Equal("original\n", File.ReadAllText(Active));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Locked_archive_aborts_without_unlocked_append()
    {
        var now = DateTime.UtcNow;
        BoundedLogFile.Append(Active, new string('x', 90), 128, 256, now);
        BoundedLogFile.Append(Active, new string('y', 90), 128, 256, now);
        var archive = BoundedLogFile.Files(_dir, "mcp-calls").Single(f => f != Active);
        File.SetLastWriteTimeUtc(archive, now.AddDays(-31));
        using (var held = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => BoundedLogFile.Append(Active, "never-written", 128, 256, now));
        Assert.DoesNotContain("never-written", string.Join("", Directory.GetFiles(_dir).Select(File.ReadAllText)));
    }

    [Fact]
    public void Startup_maintenance_when_disabled_preserves_a_fresh_oversized_legacy_record_file()
    {
        NwdCallLog.DirectoryOverride = _dir;
        NwdCallLog.SetEnabled(false);
        using (var stream = File.Create(Active)) stream.SetLength(BoundedLogFile.FileBytes + 200);
        File.AppendAllText(Active, "\n{\"recent\":true}\n", new UTF8Encoding(false));
        var before = File.ReadAllBytes(Active);
        var unrelated = Path.Combine(_dir, "owner.jsonl");
        File.WriteAllText(unrelated, "keep");
        NwdCallLog.Maintain();
        Assert.False(File.Exists(Active));
        var archive = Assert.Single(BoundedLogFile.Files(_dir, "mcp-calls"));
        Assert.Equal(before, File.ReadAllBytes(archive));
        Assert.Equal("keep", File.ReadAllText(unrelated));
        NwdCallLog.Record("get_document_info", "{}", true, 1, null, "{}");
        Assert.False(File.Exists(Active));
        Assert.Equal(before, File.ReadAllBytes(archive));
    }

    [Fact]
    public void Linked_directory_is_refused_without_touching_target()
    {
        var target = Path.Combine(_dir, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(_dir, "link");
        Directory.CreateSymbolicLink(link, target);
        try
        {
            var path = Path.Combine(link, NwdCallLog.FileName);
            Assert.Throws<IOException>(() => BoundedLogFile.Append(path, "no"));
            Assert.Empty(Directory.GetFiles(target));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void Dangling_file_link_is_refused_without_creating_its_target()
    {
        var target = Path.Combine(_dir, "missing-target.jsonl");
        File.CreateSymbolicLink(Active, target);
        try
        {
            Assert.Throws<IOException>(() => BoundedLogFile.Append(Active, "no"));
            Assert.False(File.Exists(target));
        }
        finally { File.Delete(Active); }
    }

    [Fact]
    public async Task Actual_gateway_startup_maintains_legacy_logs_when_recording_is_off()
    {
        var data = Path.Combine(_dir, "Bimwright", "nwd-mcp");
        Directory.CreateDirectory(data);
        var active = Path.Combine(data, "nwd-mcp-calls.jsonl");
        using (var seed = File.Create(active)) seed.SetLength(BoundedLogFile.FileBytes + 1);
        var before = File.ReadAllBytes(active);
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
        start.Environment.Remove(LogRetentionPolicy.EnvironmentVariable);
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        try
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(process.ExitCode == 0, await stderr);
            Assert.False(File.Exists(active));
            Assert.Equal(before, File.ReadAllBytes(Assert.Single(BoundedLogFile.Files(data, "nwd-mcp-calls"))));
            await stdout;
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    public void Dispose()
    {
        NwdCallLog.DirectoryOverride = null;
        NwdCallLog.SetEnabled(false);
        LogRetentionPolicy.Override = null;
        Directory.Delete(_dir, true);
    }
}
