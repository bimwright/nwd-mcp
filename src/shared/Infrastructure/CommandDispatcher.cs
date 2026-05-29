using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bimwright.Nwd.Shared.Security;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class CommandDispatcher
{
    private readonly IReadOnlyDictionary<string, INwdCommand> _commands;
    private readonly int _maxResponseBytes;
    public CommandDispatcher(IReadOnlyDictionary<string, INwdCommand> commands, int maxResponseBytes)
    { _commands = commands; _maxResponseBytes = maxResponseBytes; }

    public NwdCommandResult Dispatch(NwdCommandContext ctx, NwdCommandEnvelope env)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        if (!_commands.TryGetValue(env.Command, out var cmd))
            return NwdCommandResult.Fail(env.Id, "INVALID_ARGUMENT", $"unknown command: {env.Command}", meta);
        if (!cmd.IsReadOnly && ctx.ReadOnly)
            return NwdCommandResult.Fail(env.Id, "READ_ONLY", $"{env.Command} is a write command and the server is read-only", meta);
        if (env.Command == "send_code" && !ctx.EnableSendCode)
            return NwdCommandResult.Fail(env.Id, "SEND_CODE_DISABLED", "send_code is disabled. Enable it on the server (--enable-send-code) and the plug-in (BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1).", meta);

        try
        {
            var result = cmd.Execute(ctx, env.Params ?? new JObject());
            var serialized = JsonConvert.SerializeObject(result.Data);
            if (!ResponseSizeGuard.Check(serialized, _maxResponseBytes, out var sizeError))
                return NwdCommandResult.Fail(env.Id, sizeError!.Code, sizeError.Message, result.Meta);
            return result;
        }
        catch (Exception ex)
        {
            return NwdCommandResult.Fail(env.Id, "API_ERROR", ErrorSanitizer.Sanitize(ex), meta);
        }
    }
}
