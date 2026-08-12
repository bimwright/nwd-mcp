#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public static class ModelItemHelper
{
    public static string GetModelItemId(ModelItem item, Document doc)
    {
        if (item == null || doc == null) return string.Empty;
        var model = item.Model;
        if (model == null) return string.Empty;
        int modelIndex = doc.Models.IndexOf(model);
        if (modelIndex < 0) return string.Empty;

        var indexes = new List<int>();
        var current = item;
        while (current.Parent != null)
        {
            var parent = current.Parent;
            int childIndex = IndexOfChild(parent, current);
            if (childIndex < 0) break;
            indexes.Insert(0, childIndex);
            current = parent;
        }
        return modelIndex + ":" + string.Join(":", indexes);
    }

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
            if (!int.TryParse(parts[i], out int childIndex)) return null;
            var next = ChildAt(current, childIndex);
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    private static int IndexOfChild(ModelItem parent, ModelItem child)
    {
        if (parent == null || child == null) return -1;
        int index = 0;
        foreach (ModelItem candidate in parent.Children)
        {
            if (ReferenceEquals(candidate, child))
                return index;
            index++;
        }
        return -1;
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
