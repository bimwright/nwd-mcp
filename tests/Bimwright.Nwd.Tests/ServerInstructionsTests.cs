using System;
using System.Text;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

public sealed class ServerInstructionsTests
{
    private const int MaxInstructionBytes = 2048;
    private const int MaxToolDescriptionChars = 4096;

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

    // INSTR-ORDER: product keywords, then target -> health -> report, then the tool index, then Safety & permissions last.
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
        var tools = ToolAnnotationTests.ToolsFor(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true });
        Assert.NotEmpty(tools);
        foreach (var tool in tools)
        {
            var length = tool.Description?.Length ?? 0;
            Assert.True(length <= MaxToolDescriptionChars, $"{tool.Name} description is {length} chars");
        }
    }

    // INSTR-MOVE-TO-TOOL: per-tool detail lives in the tool's description.
    [Theory]
    [InlineData("nwd_health_check", "protected_component_reasons")]
    [InlineData("nwd_health_check", "host_busy")]
    [InlineData("nwd_switch_target", "error.target.candidates")]
    [InlineData("nwd_switch_target", "TARGET_REBIND_REQUIRED")]
    [InlineData("nwd_send_code", "result_contract=\"task_v1\"")]
    [InlineData("nwd_send_code", "data[\"key\"]")]
    [InlineData("nwd_open_file", "moves to the opened file")]
    [InlineData("nwd_get_model_tree", "output=file")]
    [InlineData("nwd_batch_get_properties", "output=file")]
    [InlineData("nwd_find_items_by_name", "output=file")]
    [InlineData("nwd_run_baked_tool", "output=file")]
    [InlineData("nwd_send_code", "auto-spills")]
    public void ToolDescriptionCarriesMovedDetail(string toolName, string detail)
    {
        var tools = ToolAnnotationTests.ToolsFor(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true });
        var tool = Assert.Single(tools, t => t.Name == toolName);
        Assert.Contains(detail, tool.Description);
        Assert.DoesNotContain(detail, ServerInstructions.Text);
    }
}
