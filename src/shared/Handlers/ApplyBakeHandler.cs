#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.ToolBaker;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class ApplyBakeHandler : INwdCommand
{
    public string Name => "apply_bake";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        try
        {
            var record = BakedToolRuntimeCommandFactory.FromApplyRequest(p);
            var data = new JObject
            {
                ["success"] = true,
                ["tool_name"] = record.Name,
                ["description"] = record.Description,
                ["params_schema"] = record.ParamsSchema,
                ["source_code"] = record.SourceCode
            };
            return NwdCommandResult.Success(Guid.Empty, data, meta);
        }
        catch (Exception ex)
        {
            var data = new JObject
            {
                ["success"] = false,
                ["error_code"] = "INVALID_ARGUMENT",
                ["message"] = ex.Message
            };
            return NwdCommandResult.Success(Guid.Empty, data, meta);
        }
    }
}
#endif
