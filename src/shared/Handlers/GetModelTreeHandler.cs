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
            if (model.RootItem == null) continue;

            var modelNode = BuildNode(model.RootItem, ModelItemHelper.GetModelItemId(model.RootItem, doc), 0, maxDepth, ref count, maxItems, ctx);
            if (modelNode != null)
            {
                modelNode["name"] = model.FileName ?? modelNode["name"]?.Value<string>() ?? "Model";
                roots.Add(modelNode);
            }
        }

        var data = new JObject { ["roots"] = roots };
        if (ctx.PastDeadline) data["truncated"] = "deadline";
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static JObject BuildNode(NW.ModelItem item, string id, int currentDepth, int maxDepth, ref int count, int maxItems, NwdCommandContext ctx)
    {
        if (item == null) return null;

        var node = new JObject
        {
            ["id"] = id,
            ["name"] = item.DisplayName ?? item.ClassDisplayName ?? "Unnamed",
            ["type"] = item.ClassDisplayName ?? "Node",
            ["has_geometry"] = item.HasGeometry
        };

        count++;

        if (currentDepth < maxDepth && ModelItemHelper.HasChildren(item) && count < maxItems)
        {
            var childrenArr = new JArray();
            int index = 0;
            foreach (var child in item.Children)
            {
                if (count >= maxItems || ctx.PastDeadline) break;
                // Ids follow the walk: numbering children here avoids a parent scan per node.
                var childId = id.Length == 0 ? string.Empty : ModelItemHelper.ChildId(id, index++);
                var childNode = BuildNode(child, childId, currentDepth + 1, maxDepth, ref count, maxItems, ctx);
                if (childNode != null)
                {
                    childrenArr.Add(childNode);
                }
            }
            node["children"] = childrenArr;
        }

        return node;
    }
}
#endif
