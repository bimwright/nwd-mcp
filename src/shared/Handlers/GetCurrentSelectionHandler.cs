#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GetCurrentSelectionHandler : INwdCommand
{
    public string Name => "get_current_selection";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var itemIds = new JArray();
        foreach (var item in doc.CurrentSelection.SelectedItems)
        {
            // Add item paths or unique ids
        }

        var data = new JObject { ["item_ids"] = itemIds };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
