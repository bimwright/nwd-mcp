#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GetCurrentViewpointHandler : INwdCommand
{
    public string Name => "get_current_viewpoint";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var vp = doc.CurrentViewpoint.CreateCopy();

        var pos = new JObject
        {
            ["x"] = vp.Position.X,
            ["y"] = vp.Position.Y,
            ["z"] = vp.Position.Z
        };

        var rot = new JObject
        {
            ["a"] = vp.Rotation.A,
            ["b"] = vp.Rotation.B,
            ["c"] = vp.Rotation.C,
            ["d"] = vp.Rotation.D
        };

        var data = new JObject
        {
            ["position"] = pos,
            ["rotation"] = rot,
            ["projection"] = vp.Projection.ToString(),
            ["focal_distance"] = vp.FocalDistance,
            ["has_lighting"] = vp.HasLighting
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
