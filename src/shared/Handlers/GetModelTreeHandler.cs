#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class GetModelTreeHandler : INwdCommand
{
    public string Name => "get_model_tree";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var maxDepth = p["max_depth"]?.Value<int>() ?? 2;
        var maxItems = p["max_items"]?.Value<int>() ?? 500;

        var roots = new JArray();
        int count = 0;
        foreach (var model in doc.Models)
        {
            if (count >= maxItems) break;
            var node = new JObject
            {
                ["name"] = model.FileName,
                ["type"] = "Model"
            };
            roots.Add(node);
            count++;
        }

        var data = new JObject { ["roots"] = roots };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
