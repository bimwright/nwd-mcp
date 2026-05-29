#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class ListSetsHandler : INwdCommand
{
    public string Name => "list_sets";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var sets = new JArray();
        WalkSets(doc.SelectionSets.RootItem, "", sets);

        var data = new JObject
        {
            ["sets"] = sets
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static void WalkSets(NW.GroupItem group, string prefix, JArray list)
    {
        if (group == null || group.Children == null) return;
        foreach (NW.SavedItem si in group.Children)
        {
            if (si is NW.SelectionSet set)
            {
                if (!set.HasSearch)
                {
                    var setNode = new JObject
                    {
                        ["name"] = prefix + set.DisplayName,
                        ["type"] = "selection",
                        ["count"] = set.ExplicitModelItems.Count
                    };
                    list.Add(setNode);
                }
                else
                {
                    var setNode = new JObject
                    {
                        ["name"] = prefix + set.DisplayName,
                        ["type"] = "search"
                    };
                    list.Add(setNode);
                }
            }
            else if (si is NW.GroupItem g)
            {
                var folderNode = new JObject
                {
                    ["name"] = prefix + g.DisplayName,
                    ["type"] = "folder"
                };
                list.Add(folderNode);
                WalkSets(g, prefix + g.DisplayName + "/", list);
            }
        }
    }
}
#endif
