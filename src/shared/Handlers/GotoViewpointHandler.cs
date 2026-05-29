#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GotoViewpointHandler : INwdCommand
{
    public string Name => "goto_viewpoint";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var viewpointId = (string?)p["viewpoint_id"];
        if (string.IsNullOrEmpty(viewpointId))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "viewpoint_id is required", meta);

        var sv = FindSavedViewpoint(doc.SavedViewpoints.RootItem, viewpointId);
        if (sv is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", $"Saved viewpoint '{viewpointId}' not found", meta);

        doc.CurrentViewpoint.CopyFrom(sv.Viewpoint);

        var data = new JObject
        {
            ["moved"] = true
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static NW.SavedViewpoint FindSavedViewpoint(NW.GroupItem root, string name)
    {
        if (root is null || string.IsNullOrEmpty(name)) return null;

        if (name.Contains("/"))
        {
            var segments = name.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            return FindBySegments(root, segments, 0);
        }

        foreach (NW.SavedItem item in root.Children)
        {
            if (item is NW.SavedViewpoint sv &&
                string.Equals(sv.DisplayName, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return sv;
            }
            if (item is NW.GroupItem g)
            {
                var hit = FindSavedViewpoint(g, name);
                if (hit != null) return hit;
            }
        }
        return null;
    }

    private static NW.SavedViewpoint FindBySegments(NW.GroupItem parent, string[] segments, int segmentIndex)
    {
        if (segmentIndex >= segments.Length) return null;
        var currentSegment = segments[segmentIndex];

        foreach (NW.SavedItem item in parent.Children)
        {
            if (string.Equals(item.DisplayName, currentSegment, System.StringComparison.OrdinalIgnoreCase))
            {
                if (segmentIndex == segments.Length - 1)
                {
                    return item as NW.SavedViewpoint;
                }
                if (item is NW.GroupItem childGroup)
                {
                    var hit = FindBySegments(childGroup, segments, segmentIndex + 1);
                    if (hit != null) return hit;
                }
            }
        }
        return null;
    }
}
#endif
