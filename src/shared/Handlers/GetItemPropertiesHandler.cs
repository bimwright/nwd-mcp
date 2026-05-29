#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GetItemPropertiesHandler : INwdCommand
{
    public string Name => "get_item_properties";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var itemId = (string?)p["item_id"];
        if (string.IsNullOrEmpty(itemId))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "item_id is required", meta);

        var target = ModelItemHelper.ResolveModelItemId(itemId, doc);
        if (target is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", $"Item '{itemId}' not found", meta);

        var categories = new JArray();
        foreach (NW.PropertyCategory cat in target.PropertyCategories)
        {
            var props = new JArray();
            foreach (NW.DataProperty prop in cat.Properties)
            {
                string valStr;
                try
                {
                    valStr = prop.Value?.IsDisplayString == true ? prop.Value.ToDisplayString() : prop.Value?.ToString() ?? string.Empty;
                }
                catch
                {
                    valStr = "(unreadable)";
                }

                var propObj = new JObject
                {
                    ["name"] = prop.DisplayName ?? prop.Name ?? "Unnamed",
                    ["value"] = valStr
                };
                props.Add(propObj);
            }

            var catObj = new JObject
            {
                ["name"] = cat.DisplayName ?? cat.Name ?? "Category",
                ["properties"] = props
            };
            categories.Add(catObj);
        }

        var data = new JObject 
        { 
            ["item_id"] = itemId,
            ["display_name"] = target.DisplayName ?? "Unnamed",
            ["class_display_name"] = target.ClassDisplayName ?? "Node",
            ["categories"] = categories 
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
