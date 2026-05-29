#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GetSelectionSetItemsHandler : INwdCommand
{
    public string Name => "get_selection_set_items";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var setId = (string?)p["set_id"];
        if (string.IsNullOrEmpty(setId))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "set_id is required", meta);

        NW.ModelItemCollection items;
        try
        {
            items = ResolveNamedSet(doc, setId);
        }
        catch (System.Exception ex)
        {
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", ex.Message, meta);
        }

        var itemIds = new JArray();
        foreach (NW.ModelItem mi in items)
        {
            var id = ModelItemHelper.GetModelItemId(mi, doc);
            if (!string.IsNullOrEmpty(id))
            {
                itemIds.Add(id);
            }
        }

        var data = new JObject
        {
            ["item_ids"] = itemIds
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static NW.SavedItem FindSetByName(NW.GroupItem root, string name)
    {
        if (root is null || string.IsNullOrEmpty(name)) return null;

        if (name.Contains("/"))
        {
            var segments = name.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            return FindSetBySegments(root, segments, 0);
        }

        foreach (NW.SavedItem item in root.Children)
        {
            if (string.Equals(item.DisplayName, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
            if (item is NW.GroupItem g)
            {
                var hit = FindSetByName(g, name);
                if (hit != null) return hit;
            }
        }
        return null;
    }

    private static NW.SavedItem FindSetBySegments(NW.GroupItem parent, string[] segments, int segmentIndex)
    {
        if (segmentIndex >= segments.Length) return null;
        var currentSegment = segments[segmentIndex];

        foreach (NW.SavedItem item in parent.Children)
        {
            if (string.Equals(item.DisplayName, currentSegment, System.StringComparison.OrdinalIgnoreCase))
            {
                if (segmentIndex == segments.Length - 1)
                {
                    return item;
                }
                if (item is NW.GroupItem childGroup)
                {
                    var hit = FindSetBySegments(childGroup, segments, segmentIndex + 1);
                    if (hit != null) return hit;
                }
            }
        }
        return null;
    }

    private static NW.ModelItemCollection ResolveNamedSet(NW.Document doc, string name)
    {
        NW.SavedItem hit = FindSetByName(doc.SelectionSets.RootItem, name);
        if (hit == null)
        {
            throw new System.InvalidOperationException($"set '{name}' not found");
        }
        if (hit is NW.SelectionSet set)
        {
            if (!set.HasSearch)
            {
                return new NW.ModelItemCollection(set.ExplicitModelItems);
            }
            return set.Search.FindAll(doc, false);
        }
        throw new System.InvalidOperationException($"'{name}' is not a selection or search set");
    }
}
#endif
