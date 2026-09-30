using System;
using Bimwright.Nwd.Shared.Files;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class ListRecentFilesHandler : INwdCommand
{
    public string Name => "list_recent_files";
    public bool IsReadOnly => true;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        if (!NavisworksRecentFiles.IsSupportedYear(ctx.NavisworksYear))
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "navisworks year is not set", meta);

        var limit = NavisworksRecentFiles.DefaultLimit;
        var limitToken = p["limit"];
        if (limitToken != null && limitToken.Type != JTokenType.Null)
            limit = limitToken.Value<int>();

        var files = NavisworksRecentFiles.Read(ctx.NavisworksYear, limit);
        var array = new JArray();
        foreach (var file in files)
        {
            array.Add(new JObject
            {
                ["index"] = file.Index,
                ["path"] = file.Path,
                ["display_name"] = file.DisplayName,
                ["pinned"] = file.Pinned,
                ["last_opened_utc"] = file.LastOpenedUtc.HasValue ? file.LastOpenedUtc.Value.ToString("o") : null,
                ["exists"] = file.Exists
            });
        }

        var data = new JObject
        {
            ["year"] = ctx.NavisworksYear,
            ["count"] = array.Count,
            ["files"] = array
        };
        return NwdCommandResult.Success(Guid.Empty, data, meta);
    }
}
