using System;
using System.Collections.Generic;
using System.Linq;

namespace Bimwright.Nwd.Server;

public static class ToolsetFilter
{
    public static readonly string[] KnownToolsets =
    {
        "meta", "query", "selection", "selection_write", "sets",
        "view", "view_write", "visibility", "code", "toolbaker", "toolbaker_write"
    };

    // everything except "code" (send-code is opt-in)
    public static readonly string[] DefaultOn =
    {
        "meta", "query", "selection", "selection_write", "sets",
        "view", "view_write", "visibility", "toolbaker", "toolbaker_write"
    };

    public static readonly string[] WriteCapable =
    {
        "selection_write", "view_write", "visibility", "code", "toolbaker_write"
    };

    public static HashSet<string> Resolve(NwdMcpConfig config)
    {
        var requested = config.Toolsets;
        var set = requested.Count == 0
            ? new HashSet<string>(DefaultOn, StringComparer.OrdinalIgnoreCase)
            : requested.Any(t => string.Equals(t, "all", StringComparison.OrdinalIgnoreCase))
                ? new HashSet<string>(KnownToolsets, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(requested, StringComparer.OrdinalIgnoreCase);

        set.IntersectWith(KnownToolsets);          // silently drop unknown names

        if (!config.EnableSendCode) set.Remove("code");
        if (!config.EnableToolBaker) { set.Remove("toolbaker"); set.Remove("toolbaker_write"); }
        if (config.ReadOnly) foreach (var w in WriteCapable) set.Remove(w);

        return set;
    }
}
