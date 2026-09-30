#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

/// <summary>
/// Item ids for one command. An id is the model index plus the child index at each level
/// ("0:" is a model root, "0:6:3" its descendant). Each parent's children are numbered once
/// and cached, so ids for many items cost O(items visited) rather than O(siblings) per item.
/// Create one per command; ids go stale when the document changes.
/// </summary>
public sealed class ModelItemIdMap
{
    private readonly Document _doc;
    private readonly Dictionary<ModelItem, string> _ids = new Dictionary<ModelItem, string>();

    public ModelItemIdMap(Document doc) => _doc = doc;

    public string IdOf(ModelItem item)
    {
        if (item == null || _doc == null) return string.Empty;
        if (_ids.TryGetValue(item, out var id)) return id;

        var parent = item.Parent;
        if (parent == null)
        {
            // ModelItem.Model is only set on a model's root item.
            var model = item.Model;
            int modelIndex = model == null ? -1 : _doc.Models.IndexOf(model);
            id = modelIndex < 0 ? string.Empty : modelIndex + ":";
            _ids[item] = id;
            return id;
        }

        var parentId = IdOf(parent);
        int index = 0;
        foreach (ModelItem sibling in parent.Children)
        {
            _ids[sibling] = parentId.Length == 0 ? string.Empty : ModelItemHelper.ChildId(parentId, index);
            index++;
        }
        return _ids.TryGetValue(item, out id) ? id : string.Empty;
    }
}

public static class ModelItemHelper
{
    /// <summary>One-off lookup. Use <see cref="ModelItemIdMap"/> when resolving many items.</summary>
    public static string GetModelItemId(ModelItem item, Document doc)
        => new ModelItemIdMap(doc).IdOf(item);

    public static string ChildId(string parentId, int index)
        => parentId.EndsWith(":", StringComparison.Ordinal) ? parentId + index : parentId + ":" + index;

    public static ModelItem ResolveModelItemId(string id, Document doc)
    {
        if (string.IsNullOrEmpty(id) || doc == null) return null;
        var parts = id.Split(':');
        if (parts.Length == 0) return null;
        if (!int.TryParse(parts[0], out int modelIndex)) return null;
        if (modelIndex < 0 || modelIndex >= doc.Models.Count) return null;

        var model = doc.Models[modelIndex];
        var current = model.RootItem;
        if (current == null) return null;

        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue; // a model root is "N:"
            if (!int.TryParse(parts[i], out int childIndex)) return null;
            var next = ChildAt(current, childIndex);
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    private static ModelItem ChildAt(ModelItem parent, int index)
    {
        if (parent == null || index < 0) return null;
        int i = 0;
        foreach (ModelItem child in parent.Children)
        {
            if (i == index) return child;
            i++;
        }
        return null;
    }

    public static bool HasChildren(ModelItem item)
    {
        if (item == null) return false;
        return item.Children.Any();
    }
}
#endif
