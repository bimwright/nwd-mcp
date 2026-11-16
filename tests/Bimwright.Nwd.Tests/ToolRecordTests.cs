using System.Diagnostics;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Logging;
using Bimwright.Nwd.Tests.Helpers;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

[Collection("Call log")]
public sealed class ToolRecordTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nwd-record-" + Guid.NewGuid().ToString("N"));
    private string DataDirectory => Path.Combine(_root, "Bimwright", "nwd-mcp");

    public ToolRecordTests()
    {
        NwdCallLog.DirectoryOverride = DataDirectory;
        NwdCallLog.SetEnabled(true); // Stored host toggle must not override a new gateway's false.
    }

    [Theory]
    [InlineData(null, false, false, false)]
    [InlineData("--enable-call-log", false, false, true)]
    [InlineData("--disable-call-log", true, false, false)]
    [InlineData("--enable-call-log", false, true, true)]
    public async Task Stdio_calls_record_only_when_enabled_including_local_tools_and_failures(
        string? flag, bool environmentEnabled, bool readOnly, bool expectedEnabled)
    {
        using var plugin = new FakePlugin();
        Directory.CreateDirectory(DataDirectory);
        File.Copy(Path.Combine(plugin.Config.DescriptorDirectory, plugin.TargetId + ".json"),
            Path.Combine(DataDirectory, plugin.TargetId + ".json"));
        plugin.Reply = env =>
        {
            var reply = env.Command == "get_item_properties"
                ? NwdCommandResult.Fail(env.Id, "FIXTURE_FAILURE", "Expected failure.", new NwdResponseMeta())
                : NwdCommandResult.Success(env.Id, new JObject { ["ok"] = true }, new NwdResponseMeta { DocumentId = FakePlugin.DocumentId });
            NwdCallLog.Record(env.Command, env.Params.ToString(Formatting.None), reply.Ok, 1,
                reply.Error?.Message, JsonConvert.SerializeObject(reply), env.RecordCalls);
            return reply;
        };
        var serverAssembly = typeof(Program).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(serverAssembly)!, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(serverAssembly);
        if (flag is not null) start.ArgumentList.Add(flag);
        if (readOnly) start.ArgumentList.Add("--read-only");
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("BIMWRIGHT_NWD_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(key);
        start.Environment["LOCALAPPDATA"] = _root;
        start.Environment["BIMWRIGHT_NWD_ENABLE_CALL_LOG"] = environmentEnabled ? "true" : "false";
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await Send(process, new JObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize",
                ["params"] = new JObject { ["protocolVersion"] = "2024-11-05", ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "record-test", ["version"] = "1" } }
            });
            await Read(process);
            await Send(process, new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
            await Send(process, new JObject { ["jsonrpc"] = "2.0", ["id"] = 2, ["method"] = "tools/list" });
            var catalog = (JArray)(await Read(process))["result"]!["tools"]!;
            Assert.Equal(readOnly ? 23 : 33, catalog.Count);
            Assert.Equal(!readOnly, catalog.Any(t => (string?)t["name"] == "nwd_send_code"));
            var calls = new List<(string Name, JObject Args, bool Failure)>
            {
                ("nwd_list_available_targets", new JObject(), false),
                ("nwd_get_document_info", new JObject(), false),
                ("nwd_get_item_properties", new JObject { ["itemId"] = "0" }, true),
                ("nwd_get_item_properties", new JObject(), true) // SDK binding failure, no plug-in call.
            };
            const string script = "private-script-for-hash-test";
            if (!readOnly)
            {
                // Writes need a confirmed document (FC-DOC-03); verify sends one health check.
                calls.Add(("nwd_switch_target", new JObject { ["targetId"] = "pid:" + Environment.ProcessId, ["verify"] = true }, false));
                calls.Add(("nwd_send_code", new JObject { ["code"] = script }, false));
            }
            for (var i = 0; i < calls.Count; i++)
            {
                var call = calls[i];
                await Send(process, new JObject { ["jsonrpc"] = "2.0", ["id"] = i + 3, ["method"] = "tools/call",
                    ["params"] = new JObject { ["name"] = call.Name, ["arguments"] = call.Args } });
                var response = await Read(process);
                Assert.Equal(call.Failure, response["result"]!.Value<bool>("isError"));
            }
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(process.ExitCode == 0, await stderr);
            var envelopes = plugin.Received.ToArray();
            Assert.Equal(readOnly ? 2 : 4, envelopes.Length);
            Assert.All(envelopes, e => Assert.Equal(expectedEnabled, e.RecordCalls));
            var gatewayPath = Path.Combine(DataDirectory, ToolCallLog.FileName);
            var hostPath = Path.Combine(DataDirectory, NwdCallLog.FileName);
            Assert.Equal(expectedEnabled, File.Exists(gatewayPath));
            Assert.Equal(expectedEnabled, File.Exists(hostPath));
            if (expectedEnabled)
            {
                var lines = File.ReadAllLines(gatewayPath).Select(JObject.Parse).ToArray();
                Assert.Equal(calls.Count, lines.Length);
                Assert.Equal(calls.Select(c => c.Name), lines.Select(l => l.Value<string>("tool")));
                Assert.Equal(2, lines.Count(l => !l.Value<bool>("ok")));
                Assert.Equal(envelopes.Length, File.ReadAllLines(hostPath).Length);
                if (!readOnly)
                {
                    var row = Assert.Single(lines, l => l.Value<string>("tool") == "nwd_send_code");
                    Assert.Equal(script.Length, row["params"]!.Value<int>("code_length"));
                    Assert.Equal(64, row["params"]!.Value<string>("code_sha256")!.Length);
                    Assert.DoesNotContain(script, File.ReadAllText(gatewayPath));
                    Assert.DoesNotContain(script, File.ReadAllText(hostPath));
                }
                Assert.DoesNotContain("auth_token", File.ReadAllText(gatewayPath));
            }
            Assert.True(NwdCallLog.Enabled); // Per-request flags never persist into the host toggle.
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    [Fact]
    public async Task Disabled_recording_leaves_existing_gateway_and_plugin_logs_unchanged()
    {
        Directory.CreateDirectory(DataDirectory);
        var gateway = Path.Combine(DataDirectory, ToolCallLog.FileName);
        var plugin = Path.Combine(DataDirectory, NwdCallLog.FileName);
        const string previous = "previous fixture entry\n";
        File.WriteAllText(gateway, previous);
        File.WriteAllText(plugin, previous);
        var result = new CallToolResult { Content = new List<ContentBlock>() };
        Assert.Same(result, await new ToolCallLog(false).InvokeAsync("nwd_list_sets", null,
            () => ValueTask.FromResult(result)));
        NwdCallLog.Record("list_sets", "{}", true, 1, null, "{}", enabled: false);
        Assert.Equal(previous, File.ReadAllText(gateway));
        Assert.Equal(previous, File.ReadAllText(plugin));
    }

    [Fact]
    public async Task Recording_preserves_exceptions_and_survives_unwritable_log_path()
    {
        var logger = new ToolCallLog(true);
        var expected = new InvalidOperationException("fixture");
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await logger.InvokeAsync("nwd_list_sets", null, () => throw expected));
        Assert.Same(expected, thrown);
        var row = JObject.Parse(File.ReadAllText(Path.Combine(DataDirectory, ToolCallLog.FileName)));
        Assert.False(row.Value<bool>("ok"));
        Assert.Equal(nameof(InvalidOperationException), row.Value<string>("error"));
        var blocked = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(blocked, "fixture");
        NwdCallLog.DirectoryOverride = blocked;
        var result = new CallToolResult { Content = new List<ContentBlock>() };
        Assert.Same(result, await logger.InvokeAsync("nwd_list_sets", null, () => ValueTask.FromResult(result)));
    }

    [Fact]
    public void Legacy_envelope_omits_override_and_explicit_false_round_trips()
    {
        var legacy = JsonConvert.DeserializeObject<NwdCommandEnvelope>("{\"command\":\"health_check\"}")!;
        Assert.Null(legacy.RecordCalls);
        Assert.DoesNotContain("record_calls", JsonConvert.SerializeObject(legacy));
        legacy.RecordCalls = false;
        Assert.False(JsonConvert.DeserializeObject<NwdCommandEnvelope>(JsonConvert.SerializeObject(legacy))!.RecordCalls);
    }

    private static Task Send(Process process, JObject request)
        => process.StandardInput.WriteLineAsync(request.ToString(Formatting.None));
    private static async Task<JObject> Read(Process process)
        => JObject.Parse(await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("Server closed stdout."));

    public void Dispose()
    {
        NwdCallLog.SetEnabled(false);
        NwdCallLog.DirectoryOverride = null;
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
