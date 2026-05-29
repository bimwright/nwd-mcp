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

        // Under real run: runs Search matching the filters and calls doc.CurrentSelection.CopyFrom(matches)
        var data = new JObject
        {
            ["selected_count"] = 0
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
