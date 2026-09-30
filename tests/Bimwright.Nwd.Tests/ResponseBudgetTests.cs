using System.Reflection;
using System.Text;
using System.Text.Json;
using Bimwright.Nwd.Server;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class ResponseBudgetTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nwd-budget-tests-" + Guid.NewGuid().ToString("N"));

    private static readonly IReadOnlyDictionary<string, bool> IsWrite =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["nwd_health_check"] = false,
            ["nwd_get_model_tree"] = false,
            ["nwd_batch_get_properties"] = false,
            ["nwd_find_items_by_name"] = false,
            ["nwd_hide_items"] = true,
            ["nwd_send_code"] = true,
            ["nwd_run_baked_tool"] = true,
        };

    private ResponseBudget Budget(IReadOnlyDictionary<string, bool>? map = null)
        => new(new ResponseSpillWriter(_dir), map ?? IsWrite);

    private static CallToolResult Result(string text)
        => new() { Content = new List<ContentBlock> { new TextContentBlock { Text = text } } };

    private static string Text(CallToolResult result)
        => ((TextContentBlock)result.Content[0]).Text!;

    private static Dictionary<string, JsonElement>? Args(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

    private static string BigObject(int bytes)
        => $$"""{"ok":true,"payload":"{{new string('x', bytes)}}"}""";

    [Fact]
    public void ReadToolSmallResultIsUnchanged()
    {
        var result = Result("""{"ok":true,"item_count":3}""");

        Budget().Apply("nwd_health_check", null, result);

        Assert.Equal("""{"ok":true,"item_count":3}""", Text(result));
        Assert.Null(result.IsError);
    }

    [Fact]
    public void ReadToolHundredKiBResultGetsResponseWarning()
    {
        var result = Result(BigObject(100 * 1024));

        Budget().Apply("nwd_health_check", null, result);

        var parsed = JObject.Parse(Text(result));
        Assert.True(parsed.Value<bool>("ok"));
        Assert.Contains("Oversized response warning", parsed.Value<string>("_response_warning"));
        Assert.Null(result.IsError);
    }

    [Fact]
    public void ReadToolOver1MiBIsRejectedAndFlagged()
    {
        var result = Result(BigObject(ResponseSizePolicy.BudgetBytes + 10));

        Budget().Apply("nwd_health_check", null, result);

        var parsed = JObject.Parse(Text(result));
        Assert.False(parsed.Value<bool>("ok"));
        Assert.Equal("RESPONSE_TOO_LARGE", (string?)parsed["error"]?["code"]);
        Assert.True(ToolErrorFlag.Apply(result).IsError == true);
    }

    [Fact]
    public void WriteToolOver1MiBCompactsTheCompletedMutation()
    {
        var data = new JObject
        {
            ["ok"] = true,
            ["hidden"] = new JArray(Enumerable.Range(0, 200000).Select(i => "0:" + i))
        }.ToString(Newtonsoft.Json.Formatting.None);
        var result = Result(data);

        Budget().Apply("nwd_hide_items", null, result);

        var parsed = JObject.Parse(Text(result));
        Assert.True(parsed.Value<bool>("ok"));
        Assert.True(parsed.Value<bool>("response_compacted"));
        Assert.True(parsed.Value<bool>("mutation_applied"));
        Assert.True(Encoding.UTF8.GetByteCount(Text(result)) <= ResponseSizePolicy.BudgetBytes);
        Assert.False(ToolErrorFlag.Apply(result).IsError == true);
    }

    [Fact]
    public void SendCodeOver1MiBAutoSpillsWithSummary()
    {
        var data = $$"""{"ok":true,"result":"{{new string('x', ResponseSizePolicy.BudgetBytes + 10)}}","stdout":"","error":null}""";
        var result = Result(data);

        Budget().Apply("nwd_send_code", Args("""{"code":"x"}"""), result);

        var text = Text(result);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= ResponseSizePolicy.BudgetBytes);
        var envelope = JObject.Parse(text);
        Assert.True(envelope.Value<bool>("ok"));
        Assert.True(envelope.Value<bool>("automatic"));
        Assert.Equal("nwd_send_code", envelope.Value<string>("tool"));
        Assert.True(File.Exists(envelope.Value<string>("path")));
        Assert.True(envelope["summary"]!.Value<bool>("ok"));
        Assert.False(ToolErrorFlag.Apply(result).IsError == true);
    }

    [Fact]
    public void SendCodeSmallResultIsUnchanged()
    {
        var result = Result("""{"ok":true,"result":1,"stdout":"","error":null}""");

        Budget().Apply("nwd_send_code", Args("""{"code":"1"}"""), result);

        Assert.Equal("""{"ok":true,"result":1,"stdout":"","error":null}""", Text(result));
        Assert.False(Directory.Exists(_dir) && Directory.GetFiles(_dir).Length > 0);
    }

    [Fact]
    public void ModelTreeOutputFileSpillsJson()
    {
        var result = Result("""{"roots":[{"item_id":"0:0","name":"a.nwd"}],"truncated":false}""");

        Budget().Apply("nwd_get_model_tree", Args("""{"output":"file"}"""), result);

        var envelope = JObject.Parse(Text(result));
        Assert.True(envelope.Value<bool>("ok"));
        Assert.Equal("file", envelope.Value<string>("output_mode"));
        Assert.False(envelope.Value<bool>("automatic"));
        Assert.Equal("nwd_get_model_tree", envelope.Value<string>("tool"));
        var path = envelope.Value<string>("path")!;
        Assert.EndsWith(".json", path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void BatchGetPropertiesOutputFileSpillsSqlite()
    {
        var result = Result("""
            {"items":[{"item_id":"0:1","categories":[{"name":"Item","properties":[{"name":"Name","value":"Pipe"}]}]}]}
            """);

        Budget().Apply("nwd_batch_get_properties", Args("""{"output":"file"}"""), result);

        var envelope = JObject.Parse(Text(result));
        var path = envelope.Value<string>("path")!;
        Assert.EndsWith(".sqlite", path);
        Assert.True(File.Exists(path));

        using var connection = new SqliteConnection("Data Source=" + path + ";Mode=ReadOnly");
        connection.Open();
        using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var reader = tables.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        Assert.Equal(new[] { "items", "items_categories", "items_categories_properties" }, names);
    }

    [Fact]
    public void FindItemsByNameOutputFileSpillsNdjson()
    {
        var result = Result("""{"count":2,"ids":["0:1","0:2"]}""");

        Budget().Apply("nwd_find_items_by_name", Args("""{"output":"file"}"""), result);

        var envelope = JObject.Parse(Text(result));
        var path = envelope.Value<string>("path")!;
        Assert.EndsWith(".ndjson", path);
        var lines = File.ReadAllLines(path);
        Assert.Equal(3, lines.Length);
        Assert.Equal("root", JObject.Parse(lines[0]).Value<string>("_record_type"));
        Assert.Equal("ids", JObject.Parse(lines[1]).Value<string>("_collection"));
    }

    [Fact]
    public void FailureTextWithOutputFileIsNotSpilled()
    {
        var result = Result("""{"ok":false,"error":{"code":"NO_TARGET","message":"x"}}""");

        Budget().Apply("nwd_get_model_tree", Args("""{"output":"file"}"""), result);

        Assert.Equal("""{"ok":false,"error":{"code":"NO_TARGET","message":"x"}}""", Text(result));
        Assert.False(Directory.Exists(_dir) && Directory.GetFiles(_dir).Length > 0);
    }

    [Fact]
    public void SpillFailureReportsCompletedOperationPerToolClass()
    {
        File.WriteAllText(_dir, "not a directory");

        var read = Result("""{"ok":true,"roots":[]}""");
        Budget().Apply("nwd_get_model_tree", Args("""{"output":"file"}"""), read);
        var readEnvelope = JObject.Parse(Text(read));
        Assert.Equal("SPILL_FAILED", (string?)readEnvelope["error"]?["code"]);
        Assert.True(readEnvelope.Value<bool>("operation_completed"));
        Assert.False(readEnvelope.Value<bool>("mutation_applied"));

        var indeterminate = Result("""{"ok":true,"result":null}""");
        Budget().Apply("nwd_run_baked_tool", Args("""{"output":"file"}"""), indeterminate);
        var bakedEnvelope = JObject.Parse(Text(indeterminate));
        Assert.Equal("SPILL_FAILED", (string?)bakedEnvelope["error"]?["code"]);
        Assert.Equal(JTokenType.Null, bakedEnvelope["mutation_applied"]!.Type);

        var writeMap = new Dictionary<string, bool>(IsWrite) { ["nwd_get_model_tree"] = true };
        var write = Result("""{"ok":true,"roots":[]}""");
        Budget(writeMap).Apply("nwd_get_model_tree", Args("""{"output":"file"}"""), write);
        var writeEnvelope = JObject.Parse(Text(write));
        Assert.True(writeEnvelope.Value<bool>("mutation_applied"));
    }

    [Fact]
    public void ResultsWithoutASingleTextBlockAreUntouched()
    {
        var empty = new CallToolResult { Content = new List<ContentBlock>() };
        Assert.Same(empty.Content, Budget().Apply("nwd_health_check", null, empty).Content);

        var twoBlocks = new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = BigObject(ResponseSizePolicy.BudgetBytes + 10) },
                new TextContentBlock { Text = "second" }
            }
        };
        var result = Budget().Apply("nwd_health_check", null, twoBlocks);
        Assert.Equal(2, result.Content.Count);
    }

    [Fact]
    public void DefaultMapMatchesToolAnnotations()
    {
        var budget = ResponseBudget.CreateDefault(new ResponseSpillWriter(_dir));
        var tools = Program.ResolveToolTypesForRegistration(
                new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .Where(a => a?.Name != null)
            .ToArray();

        foreach (var attr in tools)
            Assert.Equal(attr!.ReadOnly != true, budget.IsWrite(attr.Name!));
        Assert.True(budget.IsWrite("nwd_send_code"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
            else if (File.Exists(_dir))
                File.Delete(_dir);
        }
        catch { }
    }
}
