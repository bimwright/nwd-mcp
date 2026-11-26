using System.Diagnostics;
using System.Reflection;
using Bimwright.Nwd.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

public sealed class McpProtocolSmokeTests
{
    private static readonly string[] AlwaysLoadKeys = { "anthropic/alwaysLoad", "antigravity/alwaysLoad" };
    private static readonly string[] ExpectedAlwaysLoadTools =
    {
        "nwd_health_check", "nwd_list_available_targets", "nwd_report_task_result", "nwd_switch_target"
    };

    public static IEnumerable<object[]> ToolsetCases()
        => Program.ToolsetRegistrations.Select(registration => new object[] { registration.Name, registration.ToolType });

    [Theory]
    [MemberData(nameof(ToolsetCases))]
    public async Task EachToolsetRegistersItsDeclaredToolsOverStdio(string toolset, Type toolType)
    {
        var responses = await RunProtocolHandshake(toolset: toolset);
        var expected = toolType.GetMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var actual = ((JArray)responses[1]["result"]!["tools"]!)
            .Select(tool => (string?)tool["name"])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Server_exposes_tools_list_over_stdio()
    {
        var responses = await RunProtocolHandshake();
        Assert.NotNull(responses[0]["result"]?["capabilities"]?["tools"]);
        Assert.Equal("nwd-mcp", (string?)responses[0]["result"]?["serverInfo"]?["name"]);
        var tools = Assert.IsAssignableFrom<JArray>(responses[1]["result"]?["tools"]);
        Assert.Equal(35, tools.Count);
        Assert.Contains(tools, t => (string?)t["name"] == "nwd_list_available_targets");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Health_schema_and_runtime_evidence_are_available_over_MCP_including_read_only(
        bool readOnly, bool explicitArgument)
    {
        var responses = await RunProtocolHandshake(checkHealth: true, readOnly: readOnly, explicitArgument: explicitArgument);
        var tools = (JArray)responses[1]["result"]!["tools"]!;
        Assert.Equal(readOnly ? 25 : 35, tools.Count);
        var health = Assert.Single(tools, tool => (string?)tool["name"] == "nwd_health_check");
        Assert.Equal("boolean", (string?)health["inputSchema"]?["properties"]?["check_updates"]?["type"]);
        Assert.DoesNotContain("check_updates", health["inputSchema"]?["required"]?.Values<string>() ?? Array.Empty<string>());
        Assert.True(health["annotations"]!.Value<bool>("readOnlyHint"));
        Assert.False(health["annotations"]!.Value<bool>("destructiveHint"));
        Assert.True(health["annotations"]!.Value<bool>("openWorldHint"));
        Assert.Contains("nwd_health_check(check_updates=true)", (string?)health["description"]);
        Assert.Contains("update.notification", (string?)health["description"]);
        Assert.Contains("bundle_update_proposal_allowed", (string?)health["description"]);
        Assert.Contains("nwd_health_check(check_updates=true)", (string?)responses[0]["result"]?["instructions"]);
        // INSTR-MOVE-TO-TOOL: update-check detail moved from the instructions into the health description.
        var instructions = (string)responses[0]["result"]!["instructions"]!;
        Assert.Contains("protected_component_reasons", (string?)health["description"]);
        Assert.DoesNotContain("protected_component_reasons", instructions);
        // INSTR-MAX-2KB, INSTR-SAFETY-BLOCK (FC-SRV-06): wire instructions fit in 2,048 UTF-8 bytes and end with Safety & permissions.
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(instructions) <= 2048);
        Assert.EndsWith(ServerInstructions.SafetyAndPermissions, instructions);
        AssertWireToolDescriptionCaps(tools);
        AssertAlwaysLoadMetadata(tools, readOnly: readOnly);
        var data = JObject.Parse((string)responses[2]["result"]!["content"]![0]!["text"]!);
        Assert.Equal(Program.ServerInformationalVersion, (string?)responses[0]["result"]?["serverInfo"]?["version"]);
        Assert.Equal((string?)responses[0]["result"]?["serverInfo"]?["version"], (string?)data["mcp"]?["server_informational_version"]);
        Assert.True(JToken.DeepEquals(responses[0]["result"]?["serverInfo"]?["version"],
            data["client_verification"]?["expected_server_info"]?["version"]));
        Assert.True(JToken.DeepEquals(health["_meta"]?[HealthRuntimeInfo.ClientRuntimeMetadataKey],
            data["client_verification"]?["expected_runtime"]));
        Assert.Equal("not_observable_by_server", (string?)data["client_verification"]?["status"]);
        Assert.NotEmpty(data["mcp"]!["server_build_id"]!.Value<string>()!);
        Assert.Equal("not_checked", (string?)data["update"]?["status"]);
        Assert.Equal("check_only", (string?)data["update_guidance"]?["mode"]);
        Assert.False(data["update_guidance"]!.Value<bool>("requires_user_authorization"));
        Assert.DoesNotContain("install.ps1", data["update_guidance"]!["steps"]!.ToString());
        // The disposable profile has no Navisworks descriptors.
        Assert.False(data.Value<bool>("host_connected"));
        Assert.False(data.Value<bool>("ok"));
        Assert.True(responses[2]["result"]!.Value<bool>("isError"));
    }

    [Fact]
    public async Task Reconnect_can_detect_a_catalog_cached_from_a_previous_process_with_the_same_version()
    {
        var previous = await RunProtocolHandshake(checkHealth: true);
        var current = await RunProtocolHandshake(checkHealth: true);
        var oldTool = ((JArray)previous[1]["result"]!["tools"]!).Single(t => (string?)t["name"] == "nwd_health_check");
        var newTool = ((JArray)current[1]["result"]!["tools"]!).Single(t => (string?)t["name"] == "nwd_health_check");
        var data = JObject.Parse((string)current[2]["result"]!["content"]![0]!["text"]!);
        var expected = data["client_verification"]!["expected_runtime"]!;
        Assert.Equal((string?)previous[0]["result"]?["serverInfo"]?["version"], (string?)current[0]["result"]?["serverInfo"]?["version"]);
        Assert.False(JToken.DeepEquals(oldTool["_meta"]?[HealthRuntimeInfo.ClientRuntimeMetadataKey], expected));
        Assert.True(JToken.DeepEquals(newTool["_meta"]?[HealthRuntimeInfo.ClientRuntimeMetadataKey], expected));
    }

    // TOOL-ALWAYSLOAD, INSTR-MAX-2KB, INSTR-SAFETY-BLOCK: verify bare default and --disable-send-code launches too.
    [Theory]
    [InlineData(false, 35)]
    [InlineData(true, 34)]
    public async Task Default_and_disable_send_code_launches_publish_alwaysLoad_and_bounded_instructions(
        bool disableSendCode, int expectedCount)
    {
        var responses = await RunProtocolHandshake(bareDefault: !disableSendCode, disableSendCode: disableSendCode);
        var instructions = (string)responses[0]["result"]!["instructions"]!;
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(instructions) <= 2048);
        Assert.EndsWith(ServerInstructions.SafetyAndPermissions, instructions);
        var tools = (JArray)responses[1]["result"]!["tools"]!;
        Assert.Equal(expectedCount, tools.Count);
        AssertWireToolDescriptionCaps(tools);
        AssertAlwaysLoadMetadata(tools, readOnly: false, expectSendCode: !disableSendCode);
    }

    // INSTR-MAX-2KB, FC-SRV-06, TOOL-ALWAYSLOAD: filtered catalogs must not misdirect the agent.
    [Theory]
    [InlineData("meta", false, false, true, true)]
    [InlineData("query", false, true, false, false)]
    [InlineData("files_write", false, false, false, false)]
    [InlineData("meta", true, false, false, true)]
    public async Task Filtered_toolsets_publish_safe_startup_guidance(string toolset, bool readOnly,
        bool hasHealth, bool hasSwitch, bool hasReport)
    {
        var responses = await RunProtocolHandshake(toolset: toolset, readOnly: readOnly);
        var tools = (JArray)responses[1]["result"]!["tools"]!;
        var names = tools.Select(t => (string)t["name"]!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(hasHealth, names.Contains("nwd_health_check"));
        Assert.Equal(hasSwitch, names.Contains("nwd_switch_target"));
        Assert.Equal(hasReport, names.Contains("nwd_report_task_result"));

        var instructions = (string)responses[0]["result"]!["instructions"]!;
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(instructions) <= 2048);
        Assert.EndsWith(ServerInstructions.SafetyAndPermissions, instructions);
        Assert.Contains("Use only registered tools", instructions);
        Assert.Contains("if available, call nwd_health_check(check_updates=true)", instructions);
        Assert.Contains("If available, call nwd_report_task_result", instructions);
        if (!hasSwitch)
        {
            Assert.Contains("If switching is unavailable, start reads with --target pid:<n>", instructions);
            Assert.Contains("For writes, include meta in writable mode", instructions);
        }
        AssertWireToolDescriptionCaps(tools);
        AssertAlwaysLoadMetadata(tools, readOnly, expectSendCode: false);
    }

    private static void AssertWireToolDescriptionCaps(JArray tools)
    {
        foreach (var tool in tools)
        {
            var desc = (string?)tool["description"] ?? "";
            Assert.False(string.IsNullOrWhiteSpace(desc), $"{tool["name"]} has empty description");
            Assert.True(desc.Length <= 4096, $"{tool["name"]} description length {desc.Length} > 4096");
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(desc) <= 4096,
                $"{tool["name"]} description UTF-8 bytes > 4096");
            if (tool["inputSchema"]?["properties"] is JObject props)
                foreach (var prop in props.Properties())
                {
                    var pdesc = (string?)prop.Value["description"] ?? "";
                    Assert.True(System.Text.Encoding.UTF8.GetByteCount(pdesc) <= 2048,
                        $"{tool["name"]}.{prop.Name} description UTF-8 bytes > 2048");
                }
        }
    }

    private static void AssertAlwaysLoadMetadata(JArray tools, bool readOnly, bool expectSendCode = true)
    {
        var names = tools.Select(t => (string)t["name"]!).ToHashSet(StringComparer.Ordinal);
        var expectedAlwaysLoad = ExpectedAlwaysLoadTools.Where(names.Contains).ToArray();
        foreach (var alwaysLoadKey in AlwaysLoadKeys)
        {
            var actualAlwaysLoad = tools
                .Where(t => t["_meta"]?[alwaysLoadKey] is { Type: JTokenType.Boolean } flag && flag.Value<bool>())
                .Select(t => (string)t["name"]!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expectedAlwaysLoad, actualAlwaysLoad);
        }
        var health = tools.SingleOrDefault(t => (string?)t["name"] == "nwd_health_check");
        if (health != null)
            Assert.NotNull(health["_meta"]?[HealthRuntimeInfo.ClientRuntimeMetadataKey]);
        if (!readOnly && expectSendCode)
        {
            var sendCode = Assert.Single(tools, t => (string?)t["name"] == "nwd_send_code");
            foreach (var alwaysLoadKey in AlwaysLoadKeys)
                Assert.Null(sendCode["_meta"]?[alwaysLoadKey]);
            Assert.NotEqual(true, sendCode["_meta"]?["anthropic/requiresUserInteraction"]?.Value<bool>());
        }
    }

    private static async Task<JObject[]> RunProtocolHandshake(bool checkHealth = false,
        bool readOnly = false, bool explicitArgument = false, string toolset = "all",
        bool bareDefault = false, bool disableSendCode = false)
    {
        var serverAssembly = typeof(Program).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet", WorkingDirectory = Path.GetDirectoryName(serverAssembly)!,
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(serverAssembly);
        var args = bareDefault
            ? new[] { "--timeout-ms", "1000" }
            : disableSendCode
                ? new[] { "--disable-send-code", "--timeout-ms", "1000" }
                : new[] { "--toolsets", toolset, "--enable-send-code", "--read-only", readOnly ? "true" : "false", "--timeout-ms", "1000" };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
        foreach (var key in startInfo.Environment.Keys.Where(key => key.StartsWith("BIMWRIGHT_NWD_", StringComparison.OrdinalIgnoreCase)).ToArray())
            startInfo.Environment.Remove(key);
        var profile = Directory.CreateTempSubdirectory("nwd-protocol-");
        startInfo.Environment["LOCALAPPDATA"] = profile.FullName;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start nwd-mcp server.");
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke-test","version":"0.0"}}}""");
            var initialize = await ReadJsonResponse(process, "initialize");
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}""");
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
            var toolsList = await ReadJsonResponse(process, "tools/list");
            JObject? healthCall = null;
            if (checkHealth)
            {
                await process.StandardInput.WriteLineAsync(new JObject
                {
                    ["jsonrpc"] = "2.0", ["id"] = 3, ["method"] = "tools/call",
                    ["params"] = new JObject { ["name"] = "nwd_health_check",
                        ["arguments"] = explicitArgument ? new JObject { ["check_updates"] = false } : new JObject() },
                }.ToString(Formatting.None));
                healthCall = await ReadJsonResponse(process, "health");
            }
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var stderr = await stderrTask;
            Assert.True(process.ExitCode == 0, "nwd-mcp server exited with code " + process.ExitCode + ". Stderr: " + stderr);
            var responses = healthCall is null ? new[] { initialize, toolsList } : new[] { initialize, toolsList, healthCall };
            Assert.DoesNotContain(responses, r => r["error"] is not null);
            return responses;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            profile.Delete(recursive: true);
        }
    }

    private static async Task<JObject> ReadJsonResponse(Process process, string label)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        if (string.IsNullOrWhiteSpace(line)) throw new InvalidOperationException("No MCP response received for " + label + ".");
        return JObject.Parse(line);
    }
}
