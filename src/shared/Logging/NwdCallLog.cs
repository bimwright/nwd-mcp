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
    private static int _enabled;

    /// <summary>Test hook. Unset uses %LOCALAPPDATA%\Bimwright\nwd-mcp.</summary>
    internal static string? DirectoryOverride { get; set; }

    public static bool Enabled => Volatile.Read(ref _enabled) == 1;

    public static void SetEnabled(bool enabled)
    {
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
    }

    /// <summary>Retention is independent of recording; disabled Record calls remain no-ops.</summary>
    public static void Maintain()
    {
        try { BoundedLogFile.Maintain(DirectoryPath); }
        catch { } // Logging maintenance cannot prevent host or gateway startup.
    }

    public static string DirectoryPath
    {
        get
        {
            var path = DirectoryOverride;
            if (path == null || path.Trim().Length == 0)
            {
                return Path.Combine(
                    Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } root
                        ? root : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Bimwright", "nwd-mcp");
            }
            return path;
        }
    }

    public static void Record(string? command, string? paramsJson, bool ok, long durationMs, string? error, string? resultJson,
        bool? enabled = null, string fileName = FileName)
    {
        if (!(enabled ?? Enabled) || command == null || command.Trim().Length == 0)
            return;
        try
        {
            var entry = new JObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["tool"] = command,
                ["ok"] = ok,
                ["duration_ms"] = durationMs,
                ["error"] = Truncate(Redact(error), MaxErrorLength),
                ["params"] = SafeParams(command, paramsJson),
                ["result"] = Truncate(Redact(resultJson), MaxResultLength)
            };
            var dir = DirectoryPath;
            var path = Path.Combine(dir, fileName);
            var line = entry.ToString(Formatting.None) + "\n";
            BoundedLogFile.Append(path, line);
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

        if (string.Equals(command, "send_code", StringComparison.Ordinal) ||
            string.Equals(command, "nwd_send_code", StringComparison.Ordinal))
        {
            var code = obj["code"]?.Type == JTokenType.String ? obj["code"]!.Value<string>() : null;
            obj.Remove("code");
            if (code != null)
            {
                obj["code_length"] = code.Length;
                obj["code_sha256"] = Sha256Hex(code);
            }
            // rvt-mcp parity: script data is kept as its size and hash, never its values.
            foreach (var key in new[] { "data", "data_json" })
            {
                var data = obj[key];
                if (data == null) continue;
                obj.Remove(key);
                if (data.Type == JTokenType.Null) continue;
                var dataText = data.Type == JTokenType.String ? data.Value<string>()! : data.ToString(Formatting.None);
                obj[key + "_bytes"] = Encoding.UTF8.GetByteCount(dataText);
                obj[key + "_sha256"] = Sha256Hex(dataText);
            }
        }

        var text = obj.ToString(Formatting.None);
        if (text.Length <= MaxParamsLength)
            return obj;
        return TextToken(Truncate(text, MaxParamsLength));
    }

    /// <summary>rvt-mcp parity: errors and results are logged with secrets masked and Windows paths replaced by &lt;path&gt;.</summary>
    internal static string? Redact(string? text) => Bimwright.Nwd.Shared.Security.TextRedactor.Redact(text);

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
