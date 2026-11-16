using System.Text.Json;
using Bimwright.Nwd.Scripting;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Shared.Handlers;
using Bimwright.Nwd.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>send_code data + result_contract=task_v1 + McpTaskResult (rvt-mcp / ipt-mcp parity, plan M3).</summary>
public sealed class SendCodeTaskTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static JObject Ok(JToken? result = null) => new() { ["ok"] = true, ["error"] = null, ["result"] = result };
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    // ---- contract (ported from ipt-mcp d2d7428) ----

    [Theory]
    [InlineData(null, "legacy")]
    [InlineData("\"legacy\"", "legacy")]
    [InlineData("\"task_v1\"", "task_v1")]
    public void Contract_is_read_from_the_wire(string? json, string expected)
    {
        Assert.True(SendCodeTask.TryReadContract(json is null ? null : JToken.Parse(json), out var contract, out _));
        Assert.Equal(expected, contract);
    }

    [Theory]
    [InlineData("\"task_v2\"")]
    [InlineData("\"TASK_V1\"")]
    [InlineData("1")]
    public void Unknown_contract_is_rejected(string json)
    {
        Assert.False(SendCodeTask.TryReadContract(JToken.Parse(json), out _, out var error));
        Assert.Contains("task_v1", error);
    }

    [Fact]
    public void Pass_reports_passed_and_returns_its_value()
    {
        var pass = McpTaskResult.Pass(new { within = true });
        Assert.Same(pass.Value, SendCodeTask.ResultValue("task_v1", pass));
        var data = Ok(JObject.FromObject(pass.Value!));
        SendCodeTask.Apply(data, "task_v1", completed: true, pass);
        Assert.True((bool)data["ok"]!);
        Assert.Equal("passed", (string?)data["_task"]?["status"]);
        Assert.Null(data["_task"]?["error"]);
    }

    [Fact]
    public void Fail_fails_the_call_and_returns_its_details()
    {
        var fail = McpTaskResult.Fail("CLASH_FOUND", "2 clashes remain", new { count = 2 });
        Assert.Same(fail.Details, SendCodeTask.ResultValue("task_v1", fail));
        var data = Ok();
        SendCodeTask.Apply(data, "task_v1", completed: true, fail);
        Assert.False((bool)data["ok"]!);
        Assert.Equal("CLASH_FOUND: 2 clashes remain", (string?)data["error"]);
        Assert.Equal("failed", (string?)data["_task"]?["status"]);
        Assert.Equal("CLASH_FOUND", (string?)data["_task"]?["error"]?["code"]);
    }

    [Fact]
    public void Fail_without_a_code_uses_TASK_FAILED() => Assert.Equal("TASK_FAILED", McpTaskResult.Fail(" ", "x").ErrorCode);

    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    public void Task_v1_without_McpTaskResult_requires_one(object? returned)
    {
        var data = Ok();
        SendCodeTask.Apply(data, "task_v1", completed: true, returned);
        Assert.False((bool)data["ok"]!);
        Assert.StartsWith("TASK_RESULT_REQUIRED: ", (string?)data["error"]);
        Assert.Equal("not_reported", (string?)data["_task"]?["status"]);
    }

    [Fact]
    public void A_script_that_did_not_finish_reports_not_reported_and_keeps_its_error()
    {
        var data = new JObject { ["ok"] = false, ["error"] = "compile error: x" };
        SendCodeTask.Apply(data, "task_v1", completed: false, null);
        Assert.Equal("compile error: x", (string?)data["error"]);
        Assert.Equal("not_reported", (string?)data["_task"]?["status"]);
    }

    [Fact]
    public void Legacy_is_unchanged_except_for_a_hint_on_a_failed_flag()
    {
        var plain = Ok(new JValue(1));
        SendCodeTask.Apply(plain, "legacy", completed: true, 1);
        Assert.Equal(Ok(new JValue(1)).ToString(), plain.ToString());

        var flagged = Ok(JObject.Parse("{\"passed\":false}"));
        SendCodeTask.Apply(flagged, "legacy", completed: true, null);
        Assert.True((bool)flagged["ok"]!);
        Assert.Null(flagged["_task"]);
        Assert.Contains("task_v1", (string?)flagged["task_contract_hint"]);
    }

    // ---- script source: the same Roslyn path the plug-in uses ----

    public sealed class Globals { public string? __data_json; }

    private static Task<object> Run(string code, string? dataJson, bool taskV1 = false)
    {
        var options = ScriptOptions.Default
            .WithReferences(typeof(JToken).Assembly, typeof(McpTaskResult).Assembly)
            .WithImports(taskV1 ? new[] { "System", "System.Linq", SendCodeTask.ScriptNamespace } : new[] { "System", "System.Linq" });
        return CSharpScript.EvaluateAsync<object>(SendCodeTask.BuildSource(code, dataJson), options, new Globals { __data_json = dataJson }, typeof(Globals));
    }

    [Fact]
    public async Task Script_reads_data_by_key()
    {
        var value = await Run("(string)data[\"level\"] + \":\" + (int)data[\"count\"]", """{"level":"L2","count":3}""");
        Assert.Equal("L2:3", value);
    }

    [Fact]
    public async Task Date_strings_in_data_stay_strings()
        => Assert.Equal("2026-11-16T08:00:00+07:00", await Run("(string)data[\"at\"]", """{"at":"2026-11-16T08:00:00+07:00"}"""));

    [Fact]
    public async Task Without_data_a_script_may_declare_its_own_data_variable()
        => Assert.Equal(5, await Run("var data = 5; data", null));

    [Fact]
    public async Task Compile_errors_keep_the_callers_line_numbers()
    {
        var ex = await Assert.ThrowsAsync<CompilationErrorException>(() => Run("var a = 1;\nundefinedName", "{}"));
        Assert.Contains("(2,1)", ex.Diagnostics[0].ToString());
    }

    [Fact]
    public async Task Task_v1_script_returns_McpTaskResult()
    {
        var returned = await Run("McpTaskResult.Fail(\"BAD\", \"bad \" + (string)data[\"x\"])", """{"x":"y"}""", taskV1: true);
        var data = Ok();
        SendCodeTask.Apply(data, "task_v1", completed: true, returned);
        Assert.Equal("BAD: bad y", (string?)data["error"]);
    }

    // ---- gateway ----

    [Theory] // MCP arguments are already one JSON value; these are what System.Text.Json lets through
    [InlineData("""{"a":1,"a":2}""")]
    [InlineData("1e400")]
    public void Invalid_data_is_refused(string json)
    {
        Assert.False(CodeTools.TryData(Json(json), out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public async Task Gateway_rejects_bad_contract_or_data_without_sending()
    {
        using var plugin = new FakePlugin();
        var tools = new CodeTools(plugin.Client);
        var badContract = JObject.Parse(await tools.SendCode("1", result_contract: "task_v2", ct: Ct));
        var badData = JObject.Parse(await tools.SendCode("1", data: Json("""{"a":1,"a":2}"""), ct: Ct));

        Assert.Equal("INVALID_ARGUMENT", (string?)badContract["error"]?["code"]);
        Assert.Equal("INVALID_ARGUMENT", (string?)badData["error"]?["code"]);
        Assert.Empty(plugin.Received);
    }

    [Fact]
    public async Task Legacy_request_keeps_its_wire_shape()
    {
        using var plugin = new FakePlugin();
        await new CodeTools(plugin.Client).SendCode("1", ct: Ct);
        var p = plugin.Single().Params;
        Assert.Null(p["data_json"]);
        Assert.Null(p["result_contract"]);
    }

    [Fact]
    public async Task Data_and_task_v1_travel_on_the_wire()
    {
        using var plugin = new FakePlugin();
        await new CodeTools(plugin.Client).SendCode("1", data: Json("""{ "names": ["A", "B"], "at": "2026-11-16T08:00:00Z" }"""),
            result_contract: "task_v1", ct: Ct);
        var p = plugin.Single().Params;
        Assert.Equal("""{"names":["A","B"],"at":"2026-11-16T08:00:00Z"}""", (string?)p["data_json"]);
        Assert.Equal("task_v1", (string?)p["result_contract"]);
    }
}
