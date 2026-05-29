#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class FindItemsByNameHandler : INwdCommand
{
    public string Name => "find_items_by_name";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var name = (string?)p["name"];
        if (string.IsNullOrEmpty(name))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "name is required", meta);

        var exact = (bool?)p["exact"] ?? false;
        var maxItems = p["max_items"]?.Value<int>() ?? 500;

        var itemIds = new JArray();
        int count = 0;
        foreach (var mi in AllItems(doc))
        {
            if (count >= maxItems) break;
            var dispName = mi.DisplayName ?? string.Empty;
            bool match = exact 
                ? string.Equals(dispName, name, System.StringComparison.OrdinalIgnoreCase)
                : dispName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (match)
            {
                var id = ModelItemHelper.GetModelItemId(mi, doc);
                if (!string.IsNullOrEmpty(id))
                {
                    itemIds.Add(id);
                    count++;
                }
            }
        }

        var data = new JObject { ["item_ids"] = itemIds };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static System.Collections.Generic.IEnumerable<NW.ModelItem> AllItems(NW.Document doc)
    {
        if (doc == null || doc.Models == null) yield break;
        foreach (NW.Model m in doc.Models)
        {
            if (m.RootItem == null) continue;
            foreach (NW.ModelItem mi in m.RootItem.DescendantsAndSelf)
            {
                yield return mi;
            }
        }
    }
}
#endif
