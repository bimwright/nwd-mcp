#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class HealthCheckHandler : INwdCommand
{
    public string Name => "health_check";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        var hasDoc = doc != null;

        var data = new JObject
        {
            ["healthy"] = true,
            ["navisworks_year"] = ctx.NavisworksYear,
            ["has_document"] = hasDoc,
            ["document_title"] = doc?.Title
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
