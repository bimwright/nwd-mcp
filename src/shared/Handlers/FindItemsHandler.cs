#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class FindItemsHandler : INwdCommand
{
    public string Name => "find_items";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var filters = p["filters"];
        if (filters is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "filters parameter is required", meta);

        var maxItems = p["max_items"]?.Value<int>() ?? 500;

        var search = new NW.Search();
        search.Selection.SelectAll();
        search.Locations = NW.SearchLocations.DescendantsAndSelf;

        try
        {
            if (filters is JObject filterObj)
            {
                search.SearchConditions.Add(SearchConditionBuilder.BuildCondition(filterObj));
            }
            else if (filters is JArray filtersArr)
            {
                foreach (var f in filtersArr)
                {
                    if (f is JObject obj)
                    {
                        search.SearchConditions.Add(SearchConditionBuilder.BuildCondition(obj));
                    }
                }
            }
        }
        catch (System.ArgumentException ex)
        {
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", ex.Message, meta);
        }

        NW.ModelItemCollection matches = search.FindAll(doc, false);

        var itemIds = new JArray();
        int count = 0;
        foreach (NW.ModelItem mi in matches)
        {
            if (count >= maxItems) break;
            var id = ModelItemHelper.GetModelItemId(mi, doc);
            if (!string.IsNullOrEmpty(id))
            {
                itemIds.Add(id);
                count++;
            }
        }

        var data = new JObject { ["item_ids"] = itemIds };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
