#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class BatchGetPropertiesHandler : INwdCommand
{
    public string Name => "batch_get_properties";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var itemIds = p["item_ids"] as JArray;
        if (itemIds == null)
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "item_ids is required", meta);

        var results = new JArray();
        var data = new JObject { ["items"] = results };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
