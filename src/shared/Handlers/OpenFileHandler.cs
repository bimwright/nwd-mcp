#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using Bimwright.Nwd.Shared.Files;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Security;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class OpenFileHandler : INwdCommand
{
    public string Name => "open_file";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var check = LocalModelFile.Check(Text(p, "path"));
        if (!check.Ok)
            return NwdCommandResult.Fail(Guid.Empty, check.Code, check.Message, meta);

        var doc = NW.Application.ActiveDocument;
        if (doc == null)
            return NwdCommandResult.Fail(Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        if (doc.IsModified && !doc.IsClear && !Flag(p, "discard_changes"))
            return NwdCommandResult.Fail(Guid.Empty, "DOCUMENT_MODIFIED", "the current file has unsaved changes. Pass discard_changes true to open another file.", meta);

        bool opened;
        try
        {
            opened = doc.TryOpenFile(check.FullPath);
        }
        catch (Exception ex)
        {
            return NwdCommandResult.Fail(Guid.Empty, "OPEN_FAILED", ErrorSanitizer.Sanitize(ex), meta);
        }

        if (!opened)
            return NwdCommandResult.Fail(Guid.Empty, "OPEN_FAILED", "Navisworks could not open the file.", meta);

        return NwdCommandResult.Success(Guid.Empty, Snapshot(doc, check.FullPath, null), meta);
    }

    internal static JObject Snapshot(NW.Document doc, string path, string? mode)
    {
        var data = new JObject
        {
            ["path"] = path,
            ["title"] = doc.Title,
            ["file_name"] = doc.FileName,
            ["model_count"] = doc.Models == null ? 0 : doc.Models.Count
        };
        if (mode != null)
            data["mode"] = mode;
        return data;
    }

    internal static string? Text(JObject p, string name)
    {
        var token = p[name];
        if (token == null || token.Type == JTokenType.Null)
            return null;
        return token.Value<string>();
    }

    internal static bool Flag(JObject p, string name)
    {
        var token = p[name];
        if (token == null || token.Type == JTokenType.Null)
            return false;
        return token.Value<bool>();
    }
}
#endif
