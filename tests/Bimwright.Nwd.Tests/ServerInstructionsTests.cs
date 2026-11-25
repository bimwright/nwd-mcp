using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

public sealed class ServerInstructionsTests
{
    private const int MaxInstructionBytes = 2048;
    private const int MaxToolDescriptionChars = 4096;
    private const int MaxParameterDescriptionBytes = 2048;

    private static readonly NwdMcpConfig FullConfig = new() { Toolsets = new() { "all" }, EnableSendCode = true };
    private static readonly IReadOnlyList<Tool> AllTools = ToolAnnotationTests.ToolsFor(FullConfig);

    // INSTR-MAX-2KB: the server sets the same instructions for every startup flag combination,
    // so checking the configured value covers them all; the MCP smoke test also checks the wire value.
    [Fact]
    public void ConfiguredInstructionsFitIn2048Utf8Bytes()
    {
        var options = new McpServerOptions();
        Program.ConfigureMcpServerOptions(options);

        Assert.Equal(ServerInstructions.Text, options.ServerInstructions);
        var bytes = Encoding.UTF8.GetByteCount(options.ServerInstructions!);
        Assert.True(bytes <= MaxInstructionBytes, $"instructions are {bytes} UTF-8 bytes, cap {MaxInstructionBytes}");
    }

    // INSTR-ORDER (FC-SRV-06, BW-PERM-001 R-C): product keywords, target -> health -> report, tool index, Safety & permissions last.
    [Fact]
    public void InstructionsKeepKeywordsTargetHealthReportToolIndexSafetyOrder()
    {
        var text = ServerInstructions.Text;
        string[] markers =
        {
            "nwd-mcp - MCP gateway for Autodesk Navisworks Manage",
            "nwd_switch_target(\"pid:<n>\")",
            "nwd_health_check(check_updates=true)",
            "nwd_report_task_result",
            "Tools: ",
            "Safety & permissions"
        };

        var previous = -1;
        foreach (var marker in markers)
        {
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous, $"'{marker}' is missing or out of order");
            previous = index;
        }
        Assert.StartsWith(markers[0], text);
    }

    // INSTR-MOVE-TO-TOOL: the critical rules stay in the instructions in short form.
    [Theory]
    [InlineData("HOST_BUSY")]
    [InlineData("TARGET_INTERRUPTED")]
    [InlineData("TIMEOUT")]
    [InlineData("verify=true")]
    [InlineData("AMBIGUOUS_TARGET")]
    [InlineData("NO_TARGET")]
    [InlineData("PAIRING_REQUIRED")]
    [InlineData("DOCUMENT_CHANGED")]
    [InlineData("display units")]
    [InlineData("64 KiB")]
    [InlineData("1 MiB")]
    [InlineData("--read-only")]
    [InlineData("--target pid:<n>")]
    [InlineData("nwd_send_code is ON by default")]
    [InlineData("--disable-send-code")]
    [InlineData("BIMWRIGHT_NWD_ENABLE_SEND_CODE=0")]
    [InlineData("BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0")]
    public void InstructionsKeepCriticalRule(string rule)
        => Assert.Contains(rule, ServerInstructions.Text);

    // INSTR-MOVE-TO-TOOL: Claude Code truncates a tool description at 4,096 characters.
    [Fact]
    public void EveryToolDescriptionFitsIn4096Chars()
    {
        Assert.Equal(35, AllTools.Count);
        foreach (var tool in AllTools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), $"{tool.Name} has an empty description");
            var desc = tool.Description!;
            Assert.True(desc.Length <= MaxToolDescriptionChars, $"{tool.Name} description is {desc.Length} chars");
            var bytes = Encoding.UTF8.GetByteCount(desc);
            Assert.True(bytes <= MaxToolDescriptionChars, $"{tool.Name} description is {bytes} UTF-8 bytes");
        }
    }

    [Fact]
    public void EveryToolParameterDescriptionFitsIn2048Utf8Bytes()
    {
        foreach (var method in Program.ResolveToolMethodsForRegistration(FullConfig))
        {
            var toolName = method.GetCustomAttribute<McpServerToolAttribute>()!.Name;
            foreach (var param in method.GetParameters())
            {
                var desc = param.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var bytes = Encoding.UTF8.GetByteCount(desc);
                Assert.True(bytes <= MaxParameterDescriptionBytes,
                    $"{toolName}.{param.Name} description is {bytes} UTF-8 bytes, cap {MaxParameterDescriptionBytes}");
            }
        }
    }

    [Fact]
    public void EveryToolNameInInstructionsIsARealTool()
    {
        var names = AllTools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(ServerInstructions.Text, @"nwd_[a-z0-9_]+[a-z0-9]"))
            Assert.Contains(m.Value, names);
    }

    // INSTR-MOVE-TO-TOOL: per-tool detail lives in the tool's description.
    [Theory]
    [InlineData("nwd_health_check", "protected_component_reasons")]
    [InlineData("nwd_health_check", "host_busy")]
    [InlineData("nwd_switch_target", "error.target.candidates")]
    [InlineData("nwd_switch_target", "TARGET_REBIND_REQUIRED")]
    [InlineData("nwd_switch_target", "version mismatch")]
    [InlineData("nwd_send_code", "result_contract=\"task_v1\"")]
    [InlineData("nwd_send_code", "data[\"key\"]")]
    [InlineData("nwd_open_file", "moves to the opened file")]
    [InlineData("nwd_import_model", "write confirmation moves")]
    [InlineData("nwd_get_model_tree", "output=file")]
    [InlineData("nwd_batch_get_properties", "output=file")]
    [InlineData("nwd_find_items_by_name", "output=file")]
    [InlineData("nwd_run_baked_tool", "output=file")]
    [InlineData("nwd_send_code", "auto-spills")]
    public void ToolDescriptionCarriesMovedDetail(string toolName, string detail)
    {
        var tool = Assert.Single(AllTools, t => t.Name == toolName);
        Assert.Contains(detail, tool.Description);
        Assert.DoesNotContain(detail, ServerInstructions.Text);
    }

    [Theory]
    [InlineData("nwd_get_model_tree")]
    [InlineData("nwd_batch_get_properties")]
    [InlineData("nwd_find_items_by_name")]
    [InlineData("nwd_run_baked_tool")]
    public void SpillToolDescriptionsMatchDefault36HourRetention(string toolName)
    {
        var tool = Assert.Single(AllTools, t => t.Name == toolName);
        Assert.Contains("kept 36 h", tool.Description);
        Assert.DoesNotContain("kept 24 h", tool.Description);
    }
}
