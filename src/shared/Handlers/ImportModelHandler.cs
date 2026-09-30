#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using Bimwright.Nwd.Shared.Files;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Security;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class ImportModelHandler : INwdCommand
{
    public string Name => "import_model";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var mode = OpenFileHandler.Text(p, "mode");
        if (mode == null || mode.Trim().Length == 0)
            mode = "append";
        mode = mode.Trim().ToLowerInvariant();
        if (mode != "append" && mode != "merge")
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "mode must be append or merge", meta);

        var check = LocalModelFile.Check(OpenFileHandler.Text(p, "path"));
        if (!check.Ok)
            return NwdCommandResult.Fail(Guid.Empty, check.Code, check.Message, meta);

        var doc = NW.Application.ActiveDocument;
        if (doc == null)
            return NwdCommandResult.Fail(Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        bool imported;
        try
        {
            imported = mode == "merge" ? doc.TryMergeFile(check.FullPath) : doc.TryAppendFile(check.FullPath);
        }
        catch (Exception ex)
        {
            return NwdCommandResult.Fail(Guid.Empty, "IMPORT_FAILED", ErrorSanitizer.Sanitize(ex), meta);
        }

        if (!imported)
            return NwdCommandResult.Fail(Guid.Empty, "IMPORT_FAILED", "Navisworks could not import the file.", meta);

        return NwdCommandResult.Success(Guid.Empty, OpenFileHandler.Snapshot(doc, check.FullPath, mode), meta);
    }
}
#endif
