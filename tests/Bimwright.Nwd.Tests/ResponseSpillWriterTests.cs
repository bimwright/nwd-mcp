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

    private string Seed(string name, DateTime lastWriteUtc)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, "{}");
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    [Fact]
    public void DefaultRetentionKeeps35HourFileAndDeletes37HourFile()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var young = Seed("young.json", now.AddHours(-35));
        var old = Seed("old.json", now.AddHours(-37));

        var writer = new ResponseSpillWriter(_directory);
        var cleanup = writer.Cleanup(now);

        Assert.Equal(TimeSpan.FromHours(36), writer.Retention);
        Assert.Equal(1, cleanup.DeletedCount);
        Assert.True(File.Exists(young));
        Assert.False(File.Exists(old));
    }

    [Fact]
    public void NoFileCountCapDeletesFilesYoungerThanRetention()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 120; i++)
            Seed("spill-" + i + ".json", now.AddMinutes(-i * 15)); // newest 0 h, oldest ~29.75 h

        var cleanup = new ResponseSpillWriter(_directory).Cleanup(now);

        Assert.Equal(0, cleanup.DeletedCount);
        Assert.Equal(120, cleanup.RemainingCount);
    }

    [Fact]
    public void CustomRetentionIsHonoured()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var kept = Seed("47h.json", now.AddHours(-47));
        var removed = Seed("49h.json", now.AddHours(-49));

        var cleanup = new ResponseSpillWriter(_directory, retentionHours: 48).Cleanup(now);

        Assert.Equal(1, cleanup.DeletedCount);
        Assert.True(File.Exists(kept));
        Assert.False(File.Exists(removed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(24 * 365 + 1)]
    public void OutOfRangeRetentionFallsBackTo36Hours(int hours)
        => Assert.Equal(TimeSpan.FromHours(36), new ResponseSpillWriter(_directory, hours).Retention);

    [Fact]
    public void EveryNewSpillRunsCleanupAndKeepsYoungFiles()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 60; i++)
            Seed("existing-" + i + ".json", now.AddMinutes(-i - 1));
        var expired = Seed("expired.json", now.AddHours(-40));

        var spill = new ResponseSpillWriter(_directory).Write(
            "nwd_get_model_tree",
            new JObject { ["ok"] = true },
            ResponseSpillFormat.Json);

        Assert.True(File.Exists(spill.Path));
        Assert.False(File.Exists(expired));
        Assert.Equal(61, Directory.GetFiles(_directory).Length);
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
