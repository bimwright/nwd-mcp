using System;
using System.Text.RegularExpressions;

namespace Bimwright.Nwd.Shared.Security;

public static class ErrorSanitizer
{
    public static string Sanitize(Exception ex)
        => Sanitize(ex.Message ?? ex.GetType().Name);

    /// <summary>One line, Windows file paths replaced by &lt;path&gt;, secrets masked.</summary>
    public static string Sanitize(string message)
    {
        var msg = message.Replace("\r", " ").Replace("\n", " ").Trim();
        // strip Windows file paths
        msg = Regex.Replace(msg, @"[A-Za-z]:\\[^ ]+", "<path>");
        return SecretMasker.Mask(msg);
    }
}
