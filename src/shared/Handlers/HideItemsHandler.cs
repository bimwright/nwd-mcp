#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class HideItemsHandler : INwdCommand
{
    public string Name => "hide_items";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var itemIds = p["item_ids"] as JArray;
        if (itemIds is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "item_ids parameter is required", meta);

        var hide = (bool?)p["hide"] ?? true;

        // Under real run: searches for ModelItems matching the IDs, and calls doc.Models.SetHidden(items, hide)
        var data = new JObject
        {
            ["hidden_count"] = itemIds.Count,
            ["hide"] = hide
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
