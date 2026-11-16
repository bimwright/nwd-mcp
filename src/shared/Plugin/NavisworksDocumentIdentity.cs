#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using Bimwright.Nwd.Shared.Infrastructure;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Lifetime id of the document open in this Navisworks (document_target_v1). Navisworks keeps one document per
/// process, so the id changes whenever that document is replaced: a new file opened (even the same path again),
/// File &gt; New, the active document swapped, or the file name cleared: appending or merging a model into a saved
/// file makes it "Untitled", so it gets a new id too (nwd_import_model moves the confirmation itself).
/// UI thread only, like every Navisworks API call.
/// </summary>
public static class NavisworksDocumentIdentity
{
    private static readonly object Gate = new();
    private static string? _id;
    private static string? _key;
    private static NW.Document? _hooked;

    public static void Attach()
    {
        try
        {
            NW.Application.ActiveDocumentChanged -= OnActiveDocumentChanged;
            NW.Application.ActiveDocumentChanged += OnActiveDocumentChanged;
            Rehook(NW.Application.ActiveDocument);
        }
        catch { /* identity still works by key comparison in Read */ }
    }

    public static void Detach()
    {
        try
        {
            NW.Application.ActiveDocumentChanged -= OnActiveDocumentChanged;
            Rehook(null);
        }
        catch { }
    }

    /// <summary>The open document now. Called on the UI thread right before and after a command.</summary>
    public static DocumentStamp Read()
    {
        var doc = NW.Application.ActiveDocument;
        if (doc is null || doc.IsClear)
        {
            lock (Gate) { _id = null; _key = null; }
            return DocumentStamp.None;
        }
        var key = doc.FileName ?? "";
        var title = doc.Title;
        lock (Gate)
        {
            if (_id is null || !string.Equals(_key, key, StringComparison.OrdinalIgnoreCase))
            {
                _id = Guid.NewGuid().ToString("N");
                _key = key;
            }
            return new DocumentStamp(_id, string.IsNullOrEmpty(title) ? key : title);
        }
    }

    private static void Forget() { lock (Gate) { _id = null; _key = null; } }

    private static void OnActiveDocumentChanged(object sender, EventArgs e)
    {
        Forget();
        try { Rehook(NW.Application.ActiveDocument); } catch { }
    }

    private static void OnFileNameChanged(object sender, EventArgs e) => Forget();

    private static void Rehook(NW.Document? doc)
    {
        if (ReferenceEquals(_hooked, doc)) return;
        if (_hooked != null) _hooked.FileNameChanged -= OnFileNameChanged;
        _hooked = doc;
        if (doc != null) doc.FileNameChanged += OnFileNameChanged;
    }
}
#endif
