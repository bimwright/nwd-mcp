using Bimwright.Nwd.Server;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class ResponseSpillWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nwd-spill-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void JsonSpillWritesFileAndOkEnvelope()
    {
        var writer = new ResponseSpillWriter(_directory);
        var payload = new JObject { ["roots"] = new JArray(new JObject { ["item_id"] = "0:0" }) };

        var spill = writer.Write("nwd_get_model_tree", payload, ResponseSpillFormat.Json);

        Assert.True(File.Exists(spill.Path));
        Assert.EndsWith(".json", spill.Path);
        Assert.True(spill.Envelope.Value<bool>("ok"));
        Assert.Equal("file", spill.Envelope.Value<string>("output_mode"));
        Assert.Equal(spill.Path, spill.Envelope.Value<string>("path"));
    }

    [Fact]
    public void CleanupDeletesFilesOlderThan24HoursAndCapsDirectoryAt50Newest()
    {
        Directory.CreateDirectory(_directory);
        var now = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 55; i++)
        {
            var path = Path.Combine(_directory, "spill-" + i + ".json");
            File.WriteAllText(path, "{}");
            File.SetLastWriteTimeUtc(path, now.AddMinutes(-i));
        }
        var oldPath = Path.Combine(_directory, "expired.json");
        File.WriteAllText(oldPath, "{}");
        File.SetLastWriteTimeUtc(oldPath, now.AddHours(-25));

        var cleanup = new ResponseSpillWriter(_directory).Cleanup(now);

        Assert.Equal(6, cleanup.DeletedCount);
        Assert.Equal(50, cleanup.RemainingCount);
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(Path.Combine(_directory, "spill-0.json")));
        Assert.False(File.Exists(Path.Combine(_directory, "spill-54.json")));
    }

    [Fact]
    public void EveryNewSpillRunsCleanupAndKeepsItsReturnedPath()
    {
        Directory.CreateDirectory(_directory);
        var now = DateTime.UtcNow;
        for (var i = 0; i < ResponseSpillWriter.MaxRetainedFiles; i++)
        {
            var path = Path.Combine(_directory, "existing-" + i + ".json");
            File.WriteAllText(path, "{}");
            File.SetLastWriteTimeUtc(path, now.AddMinutes(-i - 1));
        }

        var spill = new ResponseSpillWriter(_directory).Write(
            "nwd_get_model_tree",
            new JObject { ["ok"] = true },
            ResponseSpillFormat.Json);

        Assert.True(File.Exists(spill.Path));
        Assert.Equal(ResponseSpillWriter.MaxRetainedFiles, Directory.GetFiles(_directory).Length);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }
        catch { }
    }
}
