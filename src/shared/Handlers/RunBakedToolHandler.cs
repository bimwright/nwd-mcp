#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.ToolBaker;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class RunBakedToolHandler : INwdCommand
{
    public string Name => "run_baked_tool";
    public bool IsReadOnly => false;

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        if (ctx.Commands == null)
            return NwdCommandResult.Fail(Guid.Empty, "API_ERROR", "Command registry is not available for baked tool dispatch.", meta);

        var recordJson = p["tool_record"] as JObject;
        if (recordJson == null)
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "tool_record is required.", meta);

        var record = recordJson.ToObject<BakedToolRecord>();
        if (record == null)
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "tool_record is invalid.", meta);

        var validation = ToolCompiler.ValidateRecord(record);
        if (!validation.Ok)
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", validation.Error ?? "Baked tool failed validation.", meta);

        var runtimeParams = p["params"] as JObject ?? new JObject();
        if (string.Equals(record.Source, "preset", StringComparison.Ordinal))
        {
            return ExecuteOne(ctx, record.HandlerTool, Merge(ParseObject(record.FixedArgs), runtimeParams), meta);
        }

        var results = new JArray();
        foreach (var step in ParseArray(record.Sequence))
        {
            var command = CommandName(step);
            var stepParams = CommandParams(step);
            if (string.IsNullOrWhiteSpace(command))
                return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "Macro step is missing a command name.", meta);

            var result = ExecuteOne(ctx, command!, Merge(stepParams, runtimeParams), meta);
            if (!result.Ok)
                return result;
            results.Add(result.Data ?? JValue.CreateNull());
        }

        return NwdCommandResult.Success(Guid.Empty, new JObject
        {
            ["ok"] = true,
            ["tool_name"] = record.Name,
            ["results"] = results
        }, meta);
    }

    private static NwdCommandResult ExecuteOne(NwdCommandContext ctx, string command, JObject parameters, NwdResponseMeta meta)
    {
        if (!BakedToolDispatchAuthorizer.IsAllowed(command))
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "Baked tool target is not allowed: " + command, meta);

        if (ctx.Commands == null || !ctx.Commands.TryGetValue(command, out var handler))
            return NwdCommandResult.Fail(Guid.Empty, "INVALID_ARGUMENT", "Baked tool target is not registered: " + command, meta);

        return handler.Execute(ctx, parameters);
    }

    private static JObject Merge(JObject baseArgs, JObject runtimeArgs)
    {
        var merged = (JObject)baseArgs.DeepClone();
        foreach (var property in runtimeArgs.Properties())
            merged[property.Name] = property.Value.DeepClone();
        return merged;
    }

    private static JObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JObject();
        return JObject.Parse(json);
    }

    private static JArray ParseArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JArray();
        return JArray.Parse(json);
    }

    private static string? CommandName(JToken step)
    {
        if (step is JObject obj)
            return (string?)obj["cmd"];
        return step.Value<string>();
    }

    private static JObject CommandParams(JToken step)
    {
        if (step is JObject obj)
            return obj["params"] as JObject ?? new JObject();
        return new JObject();
    }
}
#endif
