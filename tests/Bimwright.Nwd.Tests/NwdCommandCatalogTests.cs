using System.Linq;
using Bimwright.Nwd.Shared.Infrastructure;

namespace Bimwright.Nwd.Tests;

public sealed class NwdCommandCatalogTests
{
    private static readonly string[] Expected =
    {
        "health_check","get_document_info","get_model_statistics","get_model_tree",
        "get_item_properties","batch_get_properties","find_items","find_items_by_name",
        "get_current_selection","clear_selection","select_items_by_search",
        "list_sets","get_selection_set_items","execute_search_set",
        "list_viewpoints","get_current_viewpoint","goto_viewpoint","save_viewpoint",
        "hide_items","unhide_all","send_code"
    };

    [Fact]
    public void CatalogHasExactlyTheExpectedCommands()
    {
        var names = NwdCommandCatalog.All.Select(c => c.Name).ToArray();
        Assert.Equal(Expected.OrderBy(x => x), names.OrderBy(x => x));
    }

    [Fact]
    public void WriteCommandsAreFlaggedNotReadOnly()
    {
        foreach (var w in new[] { "clear_selection","select_items_by_search","goto_viewpoint","save_viewpoint","hide_items","unhide_all","send_code" })
            Assert.False(NwdCommandCatalog.All.Single(c => c.Name == w).IsReadOnly, $"{w} should be write");
    }
}
