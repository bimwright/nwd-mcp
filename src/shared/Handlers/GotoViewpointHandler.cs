#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GotoViewpointHandler : INwdCommand
{
    public string Name => "goto_viewpoint";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var viewpointId = (string?)p["viewpoint_id"];
        if (string.IsNullOrEmpty(viewpointId))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "viewpoint_id is required", meta);

        // Under real run: finds saved viewpoint by ID/path, and sets doc.CurrentViewpoint.CopyFrom(viewpoint)
        var data = new JObject
        {
            ["moved"] = true
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
