using Bimwright.Nwd.Server;

namespace Bimwright.Nwd.Tests;

public sealed class ToolsetFilterTests
{
    [Fact]
    public void DefaultSurfaceIncludesCode()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig());
        foreach (var t in new[] { "meta","files","files_write","query","selection","selection_write","sets","view","view_write","visibility","code","toolbaker","toolbaker_write" })
            Assert.Contains(t, set);
    }

    [Fact]
    public void DisableSendCodeRemovesCode()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig { EnableSendCode = false });
        Assert.DoesNotContain("code", set);
    }

    [Fact]
    public void AllPlusSendCodeIncludesCode()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true });
        Assert.Contains("code", set);
    }

    [Fact]
    public void ReadOnlyRemovesWriteCapableToolsetsButKeepsMeta()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig { Toolsets = new() { "all" }, ReadOnly = true, EnableSendCode = true });
        foreach (var keep in new[] { "meta","files","query","selection","sets","view","toolbaker" })
            Assert.Contains(keep, set);
        foreach (var gone in new[] { "files_write","selection_write","view_write","visibility","code","toolbaker_write" })
            Assert.DoesNotContain(gone, set);
    }

    [Fact]
    public void DefaultMatchesEveryKnownToolset()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig());
        Assert.Equal(
            ToolsetFilter.KnownToolsets.OrderBy(x => x, StringComparer.Ordinal),
            set.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void DisableToolBakerRemovesBothBakerToolsets()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig { Toolsets = new() { "all" }, EnableToolBaker = false });
        Assert.DoesNotContain("toolbaker", set);
        Assert.DoesNotContain("toolbaker_write", set);
    }
}
