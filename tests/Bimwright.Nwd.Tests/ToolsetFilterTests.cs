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
    public void ReadOnlyFiltersToolsByAnnotationNotByToolset() // family contract D10/D11
    {
        var config = new NwdMcpConfig { Toolsets = new() { "all" }, ReadOnly = true, EnableSendCode = true };
        var names = Program.ResolveToolMethodsForRegistration(config)
            .Select(m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolAttribute>(m)!.Name)
            .ToHashSet();
        foreach (var keep in new[] { "nwd_goto_viewpoint", "nwd_clear_selection", "nwd_select_items_by_search", "nwd_list_available_targets", "nwd_get_current_target" })
            Assert.Contains(keep, names);
        foreach (var gone in new[] { "nwd_switch_target", "nwd_send_code", "nwd_hide_items", "nwd_unhide_all", "nwd_save_viewpoint", "nwd_open_file", "nwd_run_baked_tool" })
            Assert.DoesNotContain(gone, names);
        Assert.All(Program.ResolveToolMethodsForRegistration(config), m => Assert.True(Program.IsReadOnlyTool(m)));
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
