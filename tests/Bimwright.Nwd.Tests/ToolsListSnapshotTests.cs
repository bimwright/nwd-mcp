using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Tests;

public sealed class ToolsListSnapshotTests
{
    private static string[] ToolNamesFor(NwdMcpConfig config)
    {
        var names = new List<string>();
        foreach (var type in Program.ResolveToolTypesForRegistration(config))
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var attr = m.GetCustomAttribute<McpServerToolAttribute>();
                if (attr?.Name is { } n) names.Add(n);
            }
        return names.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string[] Golden(string file)
        => JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine("Golden", file)))!
           .OrderBy(x => x, StringComparer.Ordinal).ToArray();

    [Fact] public void DefaultSurfaceMatchesGolden()
        => Assert.Equal(Golden("tools-default.json"), ToolNamesFor(new NwdMcpConfig()));

    [Fact] public void FullSurfaceMatchesGolden()
        => Assert.Equal(Golden("tools-full.json"), ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true }));

    [Fact] public void ReadOnlySurfaceMatchesGolden()
        => Assert.Equal(Golden("tools-read-only.json"), ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" }, ReadOnly = true, EnableSendCode = true }));

    [Fact] public void Counts()
    {
        Assert.Equal(30, ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true }).Length);
        Assert.Equal(29, ToolNamesFor(new NwdMcpConfig()).Length);
        Assert.Equal(20, ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" }, ReadOnly = true, EnableSendCode = true }).Length);
    }
}
