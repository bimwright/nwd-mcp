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
    [Fact]
    public void RegistrationTableCoversKnownToolsetsWithoutDuplicateNamesOrTypes()
    {
        var registrations = Program.ToolsetRegistrations;
        Assert.Equal(
            ToolsetFilter.KnownToolsets.OrderBy(x => x, StringComparer.Ordinal),
            registrations.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(registrations.Length, registrations.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(registrations.Length, registrations.Select(x => x.ToolType).Distinct().Count());
    }

    private static string[] ToolNamesFor(NwdMcpConfig config)
    {
        return Program.ResolveToolMethodsForRegistration(config)
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name!)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
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
        Assert.Equal(33, ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true }).Length);
        Assert.Equal(33, ToolNamesFor(new NwdMcpConfig()).Length);
        Assert.Equal(
            ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" } }),
            ToolNamesFor(new NwdMcpConfig()));
        Assert.Equal(32, ToolNamesFor(new NwdMcpConfig { EnableSendCode = false }).Length);
        Assert.Equal(23, ToolNamesFor(new NwdMcpConfig { Toolsets = new() { "all" }, ReadOnly = true, EnableSendCode = true }).Length);
    }
}
