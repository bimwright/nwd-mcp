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
    // A drive path, plain or JSON-escaped, up to a quote, space or separator.
    private static readonly Regex WindowsPath = new(@"[A-Za-z]:(?:\\{1,2}[^\\\s""',;<>|]+)+", RegexOptions.Compiled);

    private static readonly Regex[] SecretPatterns =
    {
        new(@"sk-[A-Za-z0-9_\-]{10,}", RegexOptions.Compiled),
        new(@"(?i)bearer\s+[A-Za-z0-9_\-\.=+/]+", RegexOptions.Compiled),
        new(@"(?i)""(authorization|api[_\-]?key|password|auth[_\-]?token|token|secret)""\s*:\s*""[^""]+""", RegexOptions.Compiled),
    };

    public static string? Redact(string? text)
    {
        if (text == null) return null;
        var redacted = WindowsPath.Replace(text, "<path>");
        foreach (var pattern in SecretPatterns) redacted = pattern.Replace(redacted, "***");
        return redacted;
    }
}
