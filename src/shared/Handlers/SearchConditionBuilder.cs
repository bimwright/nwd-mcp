#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

// Shared builder for Navisworks SearchConditions from a JSON filter object.
// Used by FindItemsHandler and SelectItemsBySearchHandler so the filter grammar lives in one place.
public static class SearchConditionBuilder
{
    public static NW.SearchCondition BuildCondition(JToken filter)
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
            // EqualValue compares literally, so "*" is not a wildcard there. Use the display-string
            // matchers, case-insensitive like nwd_find_items_by_name.
            case "contains":
            case "~":
                cond = cond.DisplayStringContains(value).IgnoreStringValueCase();
                break;
            case "startswith":
                cond = cond.DisplayStringWildcard(value + "*").IgnoreStringValueCase();
                break;
            case "endswith":
                cond = cond.DisplayStringWildcard("*" + value).IgnoreStringValueCase();
                break;
            default:
                throw new System.ArgumentException(
                    $"unknown operator '{op}'. Use equals, contains, startsWith, endsWith.");
        }
        return cond;
    }
}
#endif
