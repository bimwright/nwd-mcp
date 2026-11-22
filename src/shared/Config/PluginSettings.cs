using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Config;

/// <summary>
/// Plug-in settings at %LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json.
/// BIMWRIGHT_NWD_ENABLE_TOAST wins enableToast at the next launch.
/// </summary>
public static class PluginSettings
{
    public const string EnvEnableToast = "BIMWRIGHT_NWD_ENABLE_TOAST";
    public const string EnvRecordCalls = "BIMWRIGHT_NWD_RECORD_CALLS";
    public const bool DefaultEnableToast = true;
    public const bool DefaultRecordCalls = false;
    public const int DefaultToastIdleSeconds = 20;
    public const bool DefaultShowBranding = false;
    public static readonly int[] ToastIdleChoices = { 10, 20, 30, 60 };

    /// <summary>Test hook: redirect the settings file to a fixture path.</summary>
    internal static string? FilePathOverride { get; set; }

    public static string DefaultFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bimwright", "nwd-mcp", "nwdmcp.config.json");

    private static string FilePath
    {
        get
        {
            var path = FilePathOverride;
            if (path == null || path.Trim().Length == 0)
                return DefaultFilePath;
            return path;
        }
    }

    public static bool LoadToastEnabled()
    {
        var env = ParseBool(Environment.GetEnvironmentVariable(EnvEnableToast));
        if (env.HasValue)
            return env.Value;

        return ReadEnableToast(FilePath) ?? DefaultEnableToast;
    }

    public static int NormalizeToastIdleSeconds(int seconds)
    {
        for (var i = 0; i < ToastIdleChoices.Length; i++)
        {
            if (ToastIdleChoices[i] == seconds)
                return seconds;
        }
        return DefaultToastIdleSeconds;
    }

    public static int LoadToastIdleSeconds()
    {
        var file = ReadToastIdleSeconds(FilePath);
        return file.HasValue ? NormalizeToastIdleSeconds(file.Value) : DefaultToastIdleSeconds;
    }

    public static bool LoadShowBranding()
    {
        return ReadShowBranding(FilePath) ?? DefaultShowBranding;
    }

    public static bool LoadRecordCalls()
    {
        var env = ParseBool(Environment.GetEnvironmentVariable(EnvRecordCalls));
        if (env.HasValue)
            return env.Value;
        return ReadRecordCalls(FilePath) ?? DefaultRecordCalls;
    }

    public static bool SaveEnableToast(bool enabled)
    {
        return TryUpdate(root => root["enableToast"] = enabled);
    }

    /// <summary>Persist toastIdleSeconds. Values outside 10/20/30/60 are stored as 20.</summary>
    public static bool SaveToastIdleSeconds(int seconds)
    {
        var normalized = NormalizeToastIdleSeconds(seconds);
        return TryUpdate(root => root["toastIdleSeconds"] = normalized);
    }

    public static bool SaveShowBranding(bool show)
    {
        return TryUpdate(root => root["showBranding"] = show);
    }

    public static bool SaveRecordCalls(bool record)
    {
        return TryUpdate(root => root["recordCalls"] = record);
    }

    public static ToastPositionOptions LoadToastPosition()
    {
        try
        {
            if (!File.Exists(FilePath)) return new ToastPositionOptions();
            var root = JObject.Parse(File.ReadAllText(FilePath));
            var offset = root["toastDragOffset"] as JObject;
            double? x = ReadFiniteNumber(offset?["x"]);
            double? y = ReadFiniteNumber(offset?["y"]);
            return new ToastPositionOptions(
                root["toastHorizontalAlign"]?.Type == JTokenType.String && (string?)root["toastHorizontalAlign"] == "right",
                root["toastVerticalAlign"]?.Type == JTokenType.String && (string?)root["toastVerticalAlign"] == "bottom",
                root["toastDragEnabled"]?.Type == JTokenType.Boolean && (bool)root["toastDragEnabled"]!, x, y);
        }
        catch { return new ToastPositionOptions(); }
    }

    private static double? ReadFiniteNumber(JToken? token)
    {
        if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return null;
        try
        {
            var number = token.Value<double>();
            return ToastPositionOptions.Finite(number) ? number : (double?)null;
        }
        catch { return null; }
    }

    public static bool SaveToastPosition(ToastPositionOptions options)
    {
        return TryUpdate(root =>
        {
            root["toastHorizontalAlign"] = options.Right ? "right" : "left";
            root["toastVerticalAlign"] = options.Bottom ? "bottom" : "top";
            root["toastDragEnabled"] = options.DragEnabled;
            if (options.HasOffset)
                root["toastDragOffset"] = new JObject { ["x"] = options.OffsetX.GetValueOrDefault(), ["y"] = options.OffsetY.GetValueOrDefault() };
            else
                root.Remove("toastDragOffset");
        });
    }

    /// <summary>The "Pause for today" date ("yyyy-MM-dd"), read from disk on every call so a pause set in another
    /// Navisworks applies here too. Null when missing or not a string.</summary>
    public static string? LoadToastSnoozedDate()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path))
                return null;
            var value = JObject.Parse(File.ReadAllText(path))["toastSnoozedDate"];
            return value?.Type == JTokenType.String ? (string?)value : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Saves the pause date, or removes the key when <paramref name="date"/> is null.</summary>
    public static bool SaveToastSnoozedDate(string? date)
    {
        return TryUpdate(root =>
        {
            if (date == null) root.Remove("toastSnoozedDate");
            else root["toastSnoozedDate"] = date;
        });
    }

    private static readonly object WriteGate = new object();

    private static bool TryUpdate(Action<JObject> mutate)
    {
        lock (WriteGate) return TryUpdateCore(mutate);
    }

    private static bool TryUpdateCore(Action<JObject> mutate)
    {
        var path = FilePath;
        try
        {
            JObject? root = null;
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    try { root = JToken.Parse(text) as JObject; }
                    catch { root = null; }
                    if (root == null)
                        return false;
                }
            }
            root ??= new JObject();
            mutate(root);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.Indented));
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static int? ReadToastIdleSeconds(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            return JObject.Parse(File.ReadAllText(path))["toastIdleSeconds"]?.Value<int?>();
        }
        catch
        {
            return null;
        }
    }

    internal static bool? ReadShowBranding(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            return JObject.Parse(File.ReadAllText(path))["showBranding"]?.Value<bool?>();
        }
        catch
        {
            return null;
        }
    }

    internal static bool? ReadRecordCalls(string path)
    {
        return ReadBool(path, "recordCalls");
    }

    internal static bool? ReadEnableToast(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            return JObject.Parse(File.ReadAllText(path))["enableToast"]?.Value<bool?>();
        }
        catch
        {
            return null;
        }
    }

    private static bool? ReadBool(string path, string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            return JObject.Parse(File.ReadAllText(path))[name]?.Value<bool?>();
        }
        catch
        {
            return null;
        }
    }

    internal static bool? ParseBool(string? raw)
    {
        if (raw == null || raw.Trim().Length == 0)
            return null;
        var text = raw.Trim().ToLowerInvariant();
        switch (text)
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                return false;
            default:
                return null;
        }
    }
}
