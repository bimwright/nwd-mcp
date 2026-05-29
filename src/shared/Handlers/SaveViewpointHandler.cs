#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class SaveViewpointHandler : INwdCommand
{
    public string Name => "save_viewpoint";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var name = (string?)p["name"];
        if (string.IsNullOrEmpty(name))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "name is required", meta);

        var sv = new NW.SavedViewpoint(doc.CurrentViewpoint.CreateCopy())
        {
            DisplayName = name
        };
        doc.SavedViewpoints.AddCopy(sv);

        var data = new JObject
        {
            ["saved"] = true
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
