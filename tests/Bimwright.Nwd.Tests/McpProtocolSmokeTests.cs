using System.Diagnostics;
using System.Reflection;
using Bimwright.Nwd.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

public sealed class McpProtocolSmokeTests
{
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
        Assert.Contains("protected_component_reasons", (string?)health["description"]);
        // INSTR-MAX-2KB: the instructions sent over MCP fit in 2,048 UTF-8 bytes, default and --read-only.
        Assert.True(System.Text.Encoding.UTF8.GetByteCount((string)responses[0]["result"]!["instructions"]!) <= 2048);
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
        // A live Navisworks instance may be present on the test machine. Both connection outcomes retain runtime evidence.
        if (!data.Value<bool>("host_connected"))
        {
            Assert.False(data.Value<bool>("ok"));
            Assert.True(responses[2]["result"]!.Value<bool>("isError"));
        }
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

    private static async Task<JObject[]> RunProtocolHandshake(bool checkHealth = false,
        bool readOnly = false, bool explicitArgument = false, string toolset = "all")
    {
        var serverAssembly = typeof(Program).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet", WorkingDirectory = Path.GetDirectoryName(serverAssembly)!,
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(serverAssembly);
        foreach (var arg in new[] { "--toolsets", toolset, "--enable-send-code", "--read-only", readOnly ? "true" : "false", "--timeout-ms", "1000" })
            startInfo.ArgumentList.Add(arg);
        startInfo.Environment.Remove("BIMWRIGHT_NWD_ENABLE_TOOLBAKER");

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
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<JObject> ReadJsonResponse(Process process, string label)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (string.IsNullOrWhiteSpace(line)) throw new InvalidOperationException("No MCP response received for " + label + ".");
        return JObject.Parse(line);
    }
}
