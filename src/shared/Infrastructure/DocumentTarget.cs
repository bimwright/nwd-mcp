using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Shared.Infrastructure;

/// <summary>Sent by a gateway that confirms documents (family contract FC-DOC, FC-PAIR).</summary>
public sealed class DocumentTargetEnvelope
{
    [JsonProperty("protocol")] public string Protocol { get; set; } = DocumentTargetPolicy.Protocol;

    /// <summary>The document confirmed with nwd_switch_target(verify=true); null when not confirmed.</summary>
    [JsonProperty("expected_document_id")] public string? ExpectedDocumentId { get; set; }
}

/// <summary>The document a Navisworks instance has open now: a lifetime id and its title.</summary>
public readonly struct DocumentStamp
{
    public DocumentStamp(string id, string? title) { Id = id; Title = title; }
    public string Id { get; }
    public string? Title { get; }
    public static DocumentStamp None => new(DocumentTargetPolicy.NoDocument, null);
}

/// <summary>
/// Which commands must run on the confirmed document (family contract 5.2/5.3; owner decisions 2026-11-16,
/// same as ipt-mcp): writes need a confirmed target and a paired plug-in; a write must find the confirmed
/// document still open (Navisworks has one document per process); <c>open_file</c> is exempt from the id check
/// and moves the confirmation to the file it opened (Q-B). Transient UI commands (selection, go to viewpoint)
/// count as reads (D11, FC-DOC-05). Shared by the gateway (early refusal, command not sent) and the plug-in
/// (authoritative check on the Navisworks UI thread right before the handler).
/// </summary>
public static class DocumentTargetPolicy
{
    public const string Protocol = "document_target_v1";

    /// <summary>Document id when nothing is open (cleared document).</summary>
    public const string NoDocument = "no_document";

    private static readonly HashSet<string> OpensDocument = new(StringComparer.Ordinal) { "open_file" };

    // Commands whose own success can give the confirmed document a new id: open_file replaces it, and
    // import_model (append/merge) clears the file name of a saved document, which Navisworks reports as a new
    // file ("Untitled"). Verified live on Navisworks Manage 2025, 2026-11-16.
    private static readonly HashSet<string> MovesConfirmation = new(StringComparer.Ordinal) { "open_file", "import_model" };

    // Transient UI state (D11) and writes that change no Navisworks document (apply_bake installs a baked tool).
    private static readonly HashSet<string> Exempt = new(StringComparer.Ordinal)
    {
        "goto_viewpoint", "clear_selection", "select_items_by_search", "apply_bake",
    };

    /// <summary>A write that needs a confirmed target (FC-DOC-03, FC-DOC-06) and a paired plug-in (FC-PAIR-01).</summary>
    public static bool NeedsConfirmedTarget(string command, bool isReadOnly)
        => !isReadOnly && !Exempt.Contains(command);

    public static bool OpensOrReplacesDocument(string command) => OpensDocument.Contains(command);

    /// <summary>
    /// The plug-in's decision before the handler runs: null to run, else the refusal (the command is not started).
    /// <paramref name="active"/> is null when the document cannot be read (host-free tests): only pairing is checked.
    /// </summary>
    public static (string Code, string Message)? Check(string command, bool isReadOnly,
        DocumentTargetEnvelope? target, DocumentStamp? active)
    {
        if (!NeedsConfirmedTarget(command, isReadOnly)) return null;
        if (target is null || target.Protocol != Protocol)
            return ("PAIRING_REQUIRED",
                $"{command} was not started: the MCP server does not confirm the target document ({Protocol}), which this Navisworks plug-in requires for writes. " +
                "Update the nwd-mcp server and the Navisworks plug-in together from the same release, then restart the MCP client. Reads still work.");
        if (OpensOrReplacesDocument(command) || active is not { } now) return null;
        if (string.IsNullOrEmpty(target.ExpectedDocumentId))
            return ("DOCUMENT_UNCONFIRMED",
                $"{command} was not started: no document is confirmed for writes. Confirm the open document with nwd_switch_target(\"pid:<n>\", verify=true).");
        if (!string.Equals(target.ExpectedDocumentId, now.Id, StringComparison.Ordinal))
            return ("DOCUMENT_CHANGED",
                $"{command} was not started: the open document changed since it was confirmed. Open now: " +
                (now.Id == NoDocument ? "no document" : $"'{now.Title}' (document_id {now.Id})") +
                $"; confirmed: document_id {target.ExpectedDocumentId}. Check with the user which file to change, then confirm it with " +
                "nwd_switch_target(\"pid:<n>\", verify=true).");
        return null;
    }

    /// <summary>
    /// Q-B: when open_file replaced the document, or import_model gave it a new id, and the confirmed document
    /// was the one open before the command, the confirmation moves to the new id. A file the user opened in
    /// between is never adopted.
    /// </summary>
    public static string? Repin(string command, DocumentTargetEnvelope? target, DocumentStamp before, DocumentStamp after)
        => MovesConfirmation.Contains(command) && target?.ExpectedDocumentId is { Length: > 0 } expected
            && string.Equals(expected, before.Id, StringComparison.Ordinal)
            && !string.Equals(before.Id, after.Id, StringComparison.Ordinal)
            ? after.Id : null;
}
