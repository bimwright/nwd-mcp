using System;
using System.IO;
using System.Linq;
using Bimwright.Nwd.Server.Bake;
using Bimwright.Nwd.Shared.ToolBaker;
using Bimwright.Nwd.Shared.Security;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class BakeDbTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-bake-db-" + Guid.NewGuid().ToString("N"));

    public BakeDbTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch {}
    }

    [Fact]
    public void CreatesTablesAndEnforcesConstraints()
    {
        var dbPath = Path.Combine(_dir, "bake.db");
        using var db = new BakeDb(dbPath);
        db.Migrate();

        var record = new BakedToolRecord
        {
            Name = "test_tool",
            Description = "description",
            Source = "preset",
            HandlerTool = "find_items",
            SourceCode = "preset",
            ReviewedByUser = true
        };

        Assert.True(db.TryInsertRegistryRecord(record));
        // unique key constraint check
        Assert.False(db.TryInsertRegistryRecord(record));

        var list = db.ReadRegistryRecords();
        Assert.Single(list);
        Assert.Equal("test_tool", list[0].Name);
    }

    [Fact]
    public void SuggestionsFiltering()
    {
        var dbPath = Path.Combine(_dir, "bake.db");
        using var db = new BakeDb(dbPath);
        db.Migrate();

        var s1 = new BakeSuggestionRecord { Id = "sug1", ClusterKey = "k1", Source = "macro", Title = "Sug 1", Description = "Desc 1", State = BakeSuggestionStates.Open };
        var s2 = new BakeSuggestionRecord { Id = "sug2", ClusterKey = "k2", Source = "macro", Title = "Sug 2", Description = "Desc 2", State = BakeSuggestionStates.Archived };

        db.UpsertSuggestion(s1);
        db.UpsertSuggestion(s2);

        var suggestions = db.ListSuggestions();
        Assert.Equal(2, suggestions.Count);

        var listHandler = Bimwright.Nwd.Server.Handlers.ListBakeSuggestionsHandler.Handle(db);
        var res = JObject.Parse(listHandler);
        var active = res["suggestions"] as JArray;
        Assert.NotNull(active);
        Assert.Single(active);
        Assert.Equal("sug1", (string?)active[0]["id"]);
    }

    [Fact]
    public void AuditLogMasking()
    {
        var logPath = Path.Combine(_dir, "audit.jsonl");
        var logger = new ToolBakerAuditLog(logPath);
        logger.Append("test_event", new { auth_token = "secret-token-value-123456" });

        var content = File.ReadAllText(logPath);
        var masked = SecretMasker.Mask(content);
        Assert.Contains("***", masked);
        Assert.DoesNotContain("secret-token-value", masked);
    }
}
