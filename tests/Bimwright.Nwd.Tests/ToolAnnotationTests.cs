using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

/// <summary>
/// BW-PERM-001: Claude Desktop reads readOnlyHint/destructiveHint to decide when to ask.
/// Every tool except the send_code escape hatch declares all four hints explicitly.
/// </summary>
public sealed class ToolAnnotationTests
{
    private const string SendCode = "nwd_send_code";

    private static readonly string[] Destructive =
    {
        "nwd_open_file", "nwd_hide_items", "nwd_unhide_all", "nwd_run_baked_tool", "nwd_dismiss_bake_suggestion"
    };

    internal static IReadOnlyList<Tool> ToolsFor(NwdMcpConfig config)
        => Program.ResolveToolTypesForRegistration(config)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .Select(m => McpServerTool.Create(m, _ => throw new InvalidOperationException("not invoked")).ProtocolTool)
            .ToList();

    private static IReadOnlyList<Tool> Full()
        => ToolsFor(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true });

    [Fact]
    public void EveryToolExceptSendCodeDeclaresAllHints()
    {
        foreach (var tool in Full().Where(t => t.Name != SendCode))
        {
            var a = tool.Annotations;
            Assert.True(a != null, $"{tool.Name} has no annotations");
            Assert.True(a!.ReadOnlyHint.HasValue, $"{tool.Name} ReadOnly");
            Assert.True(a.DestructiveHint.HasValue, $"{tool.Name} Destructive");
            Assert.True(a.IdempotentHint.HasValue, $"{tool.Name} Idempotent");
            Assert.False(a.OpenWorldHint ?? true, $"{tool.Name} OpenWorld must be false");
        }
    }

    [Fact]
    public void SendCodeIsOutOfScopeAndLeftUnannotated()
    {
        var sendCode = Full().Single(t => t.Name == SendCode);
        Assert.True(sendCode.Annotations?.ReadOnlyHint == null && sendCode.Annotations?.DestructiveHint == null);
    }

    [Fact]
    public void ListGetFindToolsAreReadOnly()
    {
        foreach (var tool in Full().Where(t => t.Name.StartsWith("nwd_list_") || t.Name.StartsWith("nwd_get_") || t.Name.StartsWith("nwd_find_")))
        {
            Assert.True(tool.Annotations!.ReadOnlyHint, $"{tool.Name} should be read-only");
            Assert.False(tool.Annotations.DestructiveHint, $"{tool.Name} should not be destructive");
        }
    }

    [Fact]
    public void DestructiveToolsAreMarked()
    {
        var tools = Full().ToDictionary(t => t.Name);
        foreach (var name in Destructive)
        {
            Assert.False(tools[name].Annotations!.ReadOnlyHint, $"{name} ReadOnly");
            Assert.True(tools[name].Annotations!.DestructiveHint, $"{name} Destructive");
        }
        var others = tools.Values.Where(t => t.Name != SendCode && !Destructive.Contains(t.Name));
        foreach (var tool in others)
            Assert.False(tool.Annotations!.DestructiveHint, $"{tool.Name} is not in the destructive list");
    }

    [Fact]
    public void ReadOnlySurfaceIsReadOnlyApartFromTargetSwitch()
    {
        var tools = ToolsFor(new NwdMcpConfig { Toolsets = new() { "all" }, ReadOnly = true, EnableSendCode = true });
        foreach (var tool in tools.Where(t => t.Name != "nwd_switch_target"))
            Assert.True(tool.Annotations!.ReadOnlyHint, $"{tool.Name} is exposed under --read-only");
    }

    [Fact]
    public void InstructionsEndWithSafetyBlock()
    {
        Assert.EndsWith(ServerInstructions.SafetyAndPermissions, ServerInstructions.Text);
        Assert.Contains("Safety & permissions", ServerInstructions.SafetyAndPermissions);
        Assert.Contains("nwd_send_code", ServerInstructions.SafetyAndPermissions);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("README.vi.md")]
    [InlineData("README.zh-CN.md")]
    [InlineData("README.ja.md")]
    public void ReadmeAllowListMatchesReadOnlyTools(string readme)
    {
        var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "README.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var text = System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, readme));
        var listed = System.Text.RegularExpressions.Regex.Matches(text, @"mcp__nwd-mcp__(nwd_\w+)")
            .Select(m => m.Groups[1].Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var readOnly = Full().Where(t => t.Annotations?.ReadOnlyHint == true)
            .Select(t => t.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(readOnly, listed);
    }

    [Fact]
    public void OpenFileDoesNotDiscardByDefault()
    {
        var method = typeof(Bimwright.Nwd.Server.Tools.FileWriteTools).GetMethod("OpenFile")!;
        var discard = method.GetParameters().Single(p => p.Name == "discardChanges");
        Assert.Equal(false, discard.DefaultValue);
    }
}
