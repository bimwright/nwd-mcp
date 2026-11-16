using ModelContextProtocol.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

/// <summary>
/// BW-TARGET-001 §6.10: the target payload must reach the agent whatever the tool does with the
/// exception. When the binding refused this call, add <c>error.target</c> to the tool's failed result
/// (tools and nwd_health_check keep their own fields). Successful results are left alone.
/// </summary>
internal static class TargetErrorPayload
{
    public static CallToolResult Apply(CallToolResult result)
    {
        var state = TargetCallContext.Value;
        if (state?.Failure is not { } target || state.FailureCode is not { } code)
            return result;
        if (result.Content is not { Count: 1 } || result.Content[0] is not TextContentBlock block)
            return result;

        JObject? parsed = null;
        try { parsed = JToken.Parse(block.Text ?? "") as JObject; }
        catch (JsonException) { }

        // Never rewrite a successful result: a tool may recover from one refused wire command.
        if (parsed is null || parsed.Value<bool?>("ok") != false)
            return result;
        if (parsed["error"] is JObject error && (string?)error["code"] == code)
            error["target"] ??= target.DeepClone();
        else
            parsed["target"] ??= target.DeepClone();
        block.Text = parsed.ToString(Formatting.None);
        return result;
    }
}
