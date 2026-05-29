using Bimwright.Nwd.Server;

namespace Bimwright.Nwd.Tests;

public sealed class ToolsetFilterTests
{
    [Fact]
    public void DefaultSurfaceIncludesEverythingExceptCode()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig());
        foreach (var t in new[] { "meta","query","selection","selection_write","sets","view","view_write","visibility","toolbaker","toolbaker_write" })
            Assert.Contains(t, set);
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
        foreach (var keep in new[] { "meta","query","selection","sets","view","toolbaker" })
            Assert.Contains(keep, set);
        foreach (var gone in new[] { "selection_write","view_write","visibility","code","toolbaker_write" })
            Assert.DoesNotContain(gone, set);
    }

    [Fact]
    public void DisableToolBakerRemovesBothBakerToolsets()
    {
        var set = ToolsetFilter.Resolve(new NwdMcpConfig { Toolsets = new() { "all" }, EnableToolBaker = false });
        Assert.DoesNotContain("toolbaker", set);
        Assert.DoesNotContain("toolbaker_write", set);
    }
}
