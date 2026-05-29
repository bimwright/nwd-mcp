using System;
using System.Collections.Generic;

namespace Bimwright.Nwd.Shared.Infrastructure;

public readonly record struct NwdCommandInfo(string Name, bool IsReadOnly);

public static class NwdCommandCatalog
{
    public static readonly IReadOnlyList<NwdCommandInfo> All = new[]
    {
        new NwdCommandInfo("health_check", true),
        new NwdCommandInfo("get_document_info", true),
        new NwdCommandInfo("get_model_statistics", true),
        new NwdCommandInfo("get_model_tree", true),
        new NwdCommandInfo("get_item_properties", true),
        new NwdCommandInfo("batch_get_properties", true),
        new NwdCommandInfo("find_items", true),
        new NwdCommandInfo("find_items_by_name", true),
        new NwdCommandInfo("get_current_selection", true),
        new NwdCommandInfo("clear_selection", false),
        new NwdCommandInfo("select_items_by_search", false),
        new NwdCommandInfo("list_sets", true),
        new NwdCommandInfo("get_selection_set_items", true),
        new NwdCommandInfo("execute_search_set", true),   // read; select handled in wrapper/handler
        new NwdCommandInfo("list_viewpoints", true),
        new NwdCommandInfo("get_current_viewpoint", true),
        new NwdCommandInfo("goto_viewpoint", false),
        new NwdCommandInfo("save_viewpoint", false),
        new NwdCommandInfo("hide_items", false),
        new NwdCommandInfo("unhide_all", false),
        new NwdCommandInfo("send_code", false),
    };

    public static bool TryGet(string name, out NwdCommandInfo info)
    {
        foreach (var c in All) if (c.Name == name) { info = c; return true; }
        info = default; return false;
    }
}
