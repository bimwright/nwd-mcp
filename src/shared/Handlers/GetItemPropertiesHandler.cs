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

        var categories = new JArray();
        // Traverse and read properties of target item_id
        
        var data = new JObject { ["categories"] = categories };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
