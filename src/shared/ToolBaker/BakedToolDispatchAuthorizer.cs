using System;
using System.Collections.Generic;

namespace Bimwright.Nwd.Shared.ToolBaker;

public static class BakedToolDispatchAuthorizer
{
    private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "find_items",
        "find_items_by_name",
        "get_item_properties",
        "get_model_tree",
        "list_sets",
        "execute_search_set"
    };

    private static readonly HashSet<string> Denied = new HashSet<string>(StringComparer.Ordinal)
    {
        "send_code",
        "batch_execute",
        "run_baked_tool",
        "apply_bake",
        "accept_bake_suggestion",
        "dismiss_bake_suggestion",
        "list_baked_tools"
    };

    public static bool IsAllowed(string command)
        => !string.IsNullOrWhiteSpace(command) && !Denied.Contains(command) && Allowed.Contains(command);
}
