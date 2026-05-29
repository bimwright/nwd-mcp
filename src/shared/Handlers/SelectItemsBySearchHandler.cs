#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class SelectItemsBySearchHandler : INwdCommand
{
    public string Name => "select_items_by_search";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var filters = p["filters"];
        if (filters is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "filters parameter is required", meta);

        var search = new NW.Search();
        search.Selection.SelectAll();
        search.Locations = NW.SearchLocations.DescendantsAndSelf;

        try
        {
            if (filters is JObject filterObj)
            {
                search.SearchConditions.Add(BuildCondition(filterObj));
            }
            else if (filters is JArray filtersArr)
            {
                foreach (var f in filtersArr)
                {
                    if (f is JObject obj)
                    {
                        search.SearchConditions.Add(BuildCondition(obj));
                    }
                }
            }
        }
        catch (System.ArgumentException ex)
        {
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", ex.Message, meta);
        }

        NW.ModelItemCollection matches = search.FindAll(doc, false);
        doc.CurrentSelection.CopyFrom(matches);

        var data = new JObject
        {
            ["selected_count"] = matches.Count
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static NW.SearchCondition BuildCondition(JToken filter)
    {
        var category = (string?)filter["category"] ?? "Item";
        var property = (string?)filter["property"] ?? "Name";
        var op = (string?)filter["operator"] ?? "contains";
        var value = (string?)filter["value"] ?? "";

        NW.SearchCondition cond = NW.SearchCondition.HasPropertyByDisplayName(category, property);
        switch ((op ?? "").Trim().ToLowerInvariant())
        {
            case "equals":
            case "=":
                cond = cond.EqualValue(NW.VariantData.FromDisplayString(value));
                break;
            case "contains":
            case "~":
                cond = cond.EqualValue(NW.VariantData.FromDisplayString("*" + value + "*"));
                break;
            case "startswith":
                cond = cond.EqualValue(NW.VariantData.FromDisplayString(value + "*"));
                break;
            case "endswith":
                cond = cond.EqualValue(NW.VariantData.FromDisplayString("*" + value));
                break;
            default:
                throw new System.ArgumentException(
                    $"unknown operator '{op}'. Use equals, contains, startsWith, endsWith.");
        }
        return cond;
    }
}
#endif
