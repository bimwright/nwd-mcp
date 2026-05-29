using System;

namespace Bimwright.Nwd.Shared.Plugin;

public static partial class NwdCommandRegistry
{
    public static readonly string[] RegisteredNames = new[]
    {
        "health_check",
        "get_document_info",
        "get_model_statistics",
        "get_model_tree",
        "get_item_properties",
        "batch_get_properties",
        "find_items",
        "find_items_by_name",
        "get_current_selection",
        "clear_selection",
        "select_items_by_search",
        "list_sets",
        "get_selection_set_items",
        "execute_search_set",
        "list_viewpoints",
        "get_current_viewpoint",
        "goto_viewpoint",
        "save_viewpoint",
        "hide_items",
        "unhide_all",
        "send_code"
    };
}
