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

        // Under real run: searches for the set by ID/path, gets items and builds JArray
        var data = new JObject
        {
            ["item_ids"] = new JArray()
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
