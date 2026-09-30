using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Logging;

/// <summary>
/// One JSON line per tool call when recording is on. Off writes nothing.
/// send_code stores the script length and SHA-256, not the source.
/// </summary>
public static class NwdCallLog
{
    public const string FileName = "mcp-calls.jsonl";
    private const int MaxParamsLength = 4000;
    private const int MaxResultLength = 4000;
    private const int MaxErrorLength = 2000;
    private static readonly object Gate = new object();
    private static int _enabled;

    /// <summary>Test hook. Unset uses %LOCALAPPDATA%\Bimwright\nwd-mcp.</summary>
    internal static string? DirectoryOverride { get; set; }

    public static bool Enabled => Volatile.Read(ref _enabled) == 1;

    public static void SetEnabled(bool enabled)
    {
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
    }

    public static string DirectoryPath
    {
        get
        {
            var path = DirectoryOverride;
            if (path == null || path.Trim().Length == 0)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Bimwright", "nwd-mcp");
            }
            return path;
        }
    }

    public static void Record(string? command, string? paramsJson, bool ok, long durationMs, string? error, string? resultJson)
    {
        if (!Enabled || command == null || command.Trim().Length == 0)
            return;
        try
        {
            var entry = new JObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["tool"] = command,
                ["ok"] = ok,
                ["duration_ms"] = durationMs,
                ["error"] = Truncate(error, MaxErrorLength),
                ["params"] = SafeParams(command, paramsJson),
                ["result"] = Truncate(resultJson, MaxResultLength)
            };
            var dir = DirectoryPath;
            var path = Path.Combine(dir, FileName);
            var line = entry.ToString(Formatting.None) + "\n";
            lock (Gate)
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(path, line);
            }
        }
        catch
        {
        }
    }

    internal static JToken SafeParams(string command, string? paramsJson)
    {
        if (paramsJson == null || paramsJson.Trim().Length == 0)
            return JValue.CreateNull();
        JObject? obj = null;
        try { obj = JToken.Parse(paramsJson) as JObject; }
        catch { obj = null; }
        if (obj == null)
            return TextToken(Truncate(paramsJson, MaxParamsLength));

        if (string.Equals(command, "send_code", StringComparison.Ordinal))
        {
            var code = obj["code"]?.Type == JTokenType.String ? obj["code"]!.Value<string>() : null;
            obj.Remove("code");
            if (code != null)
            {
                obj["code_length"] = code.Length;
                obj["code_sha256"] = Sha256Hex(code);
            }
        }

        var text = obj.ToString(Formatting.None);
        if (text.Length <= MaxParamsLength)
            return obj;
        return TextToken(Truncate(text, MaxParamsLength));
    }

    private static JToken TextToken(string? value)
    {
        return value == null ? JValue.CreateNull() : new JValue(value);
    }

    private static string Sha256Hex(string text)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (value == null || value.Length <= max)
            return value;
        return value.Substring(0, max);
    }
}
