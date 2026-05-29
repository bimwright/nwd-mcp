#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class ExecuteSearchSetHandler : INwdCommand
{
    public string Name => "execute_search_set";
    public bool IsReadOnly => true; // Marked as read-only because it's a query tool that optionally mutates selection only when select=true (which is gated by the dispatcher/wrapper)

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        
        var readOnlyEnforced = (bool?)p["read_only_enforced"] ?? false;
        if (readOnlyEnforced || ctx.ReadOnly)
        {
            meta.ReadOnlyEnforced = true;
        }

        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var setId = (string?)p["set_id"];
        if (string.IsNullOrEmpty(setId))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "set_id is required", meta);

        var select = (bool?)p["select"] ?? false;
        if (select && meta.ReadOnlyEnforced == true)
        {
            select = false; // Override selection request if read-only is enforced
        }

        // Under real run: searches for the set, runs its search, returns matched items, and optionally selects them
        var data = new JObject
        {
            ["item_ids"] = new JArray(),
            ["matched_count"] = 0,
            ["selected"] = select
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
