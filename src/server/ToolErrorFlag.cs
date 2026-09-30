using System.Linq;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

/// <summary>
/// Tools report failures as JSON text (<c>{"ok":false,...}</c>). Mirror that into the MCP
/// <c>isError</c> flag so clients and agents can tell a failed call without parsing the text.
/// </summary>
public static class ToolErrorFlag
{
    public static CallToolResult Apply(CallToolResult result)
    {
        if (result.IsError == true)
            return result;
        var text = result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (IsFailure(text))
            result.IsError = true;
        return result;
    }

    public static bool IsFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.TrimStart()[0] != '{')
            return false;
        try
        {
            var ok = JObject.Parse(text)["ok"];
            return ok != null && ok.Type == JTokenType.Boolean && !ok.Value<bool>();
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return false;
        }
    }
}
