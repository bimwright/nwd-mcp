using System.Text.RegularExpressions;

namespace Bimwright.Nwd.Shared.Security;

/// <summary>
/// rvt-mcp's targeted redaction for send_code errors and call-log text: Windows paths become &lt;path&gt;, and
/// <c>sk-</c> keys, bearer tokens and authorization/api_key/password/token/secret fields become ***.
/// Unlike <see cref="SecretMasker"/> it has no blanket long-word mask, which would also hide identifiers
/// such as InvalidOperationException or a long variable name in a compile error.
/// </summary>
public static class TextRedactor
{
    // rvt-mcp's path patterns, in its order: JSON-escaped UNC and drive paths first, then plain ones. Segments may
    // contain spaces, so a path is replaced whole (an unquoted path also takes the text after it on that line).
    private static readonly Regex[] PathPatterns =
    {
        new(@"(?i)\\\\\\\\[^\\/:*?""<>|\r\n]+(?:\\\\[^\\/:*?""<>|\r\n]+)+", RegexOptions.Compiled),
        new(@"(?i)\b[A-Z]:(?:\\\\[^\\/:*?""<>|\r\n]+)+", RegexOptions.Compiled),
        new(@"(?i)\\\\[^\\/:*?""<>|\r\n]+(?:\\[^\\/:*?""<>|\r\n]+)+", RegexOptions.Compiled),
        new(@"(?i)\b[A-Z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]+", RegexOptions.Compiled),
    };

    private static readonly Regex[] SecretPatterns =
    {
        new(@"sk-[A-Za-z0-9_\-]{10,}", RegexOptions.Compiled),
        new(@"(?i)bearer\s+[A-Za-z0-9_\-\.=+/]+", RegexOptions.Compiled),
        new(@"(?i)""(authorization|api[_\-]?key|password|auth[_\-]?token|token|secret)""\s*:\s*""[^""]+""", RegexOptions.Compiled),
    };

    public static string? Redact(string? text)
    {
        if (text == null) return null;
        var redacted = text;
        foreach (var pattern in PathPatterns) redacted = pattern.Replace(redacted, "<path>");
        foreach (var pattern in SecretPatterns) redacted = pattern.Replace(redacted, "***");
        return redacted;
    }
}
