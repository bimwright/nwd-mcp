using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bimwright.Nwd.Shared.Localization;

/// <summary>
/// English-only string table for plug-in UI. Same call shape as the other
/// gateways: L.T(key, (name, value)...) with "{name}" or "{name:n}".
/// A missing key returns the key.
/// </summary>
public static class L
{
    private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["toast.connected.title"] = "Agent connected",
        ["toast.connected.summary"] = "nwd-mcp is ready",
        ["toast.failed.default"] = "Tool call failed",
        ["toast.capture.saved"] = "Saved {fileName}",
        ["toast.capture.savedSize"] = "Saved {fileName} · {width}×{height} {format}",
        ["toast.capture.id"] = "Capture {id}",
        ["toast.capture.clickToOpen"] = "Click to open",
        ["toast.capture.imageFallback"] = "image",
        ["toast.document.untitled"] = "Untitled model",
        ["toast.document.models"] = "Models: {count:n}",
        ["toast.sendCode.finished"] = "Script finished",
        ["toast.sendCode.detail"] = "Custom C# executed in Navisworks",
        ["toast.generic.completed"] = "Completed successfully",
        ["toast.generic.results"] = "Results: {count:n}",
        ["toast.generic.items"] = "Items: {count:n}",
        ["toast.generic.rows"] = "Rows: {count:n}",
        ["toast.generic.fileFallback"] = "file",
        ["toast.activity.success"] = "Success",
        ["toast.activity.failed"] = "Failed",
        ["toast.activity.capture"] = "Capture",
        ["toast.activity.open_image"] = "Open image",
        ["toast.activity.counts"] = "Counts are commands in this card. Capture is how many of the successes saved an image.",
        ["toast.status.enabled"] = "Toast notifications enabled",
        ["toast.status.enabled.summary"] = "New activity will appear here.",
        ["toast.status.disabled"] = "Toast notifications disabled",
        ["toast.status.disabled.summary"] = "New activity is hidden until toast notifications are enabled.",
        ["toast.status.saveFailed"] = "Preference could not be saved; this session is still using the new state.",
        ["toast.status.window"] = "Activity toast",
        ["settings.toast.enabled"] = "Show activity notifications",
        ["settings.toast.enabled.help"] = "Takes effect immediately.",
        ["settings.toast.brand"] = "Show branding",
        ["settings.toast.brand.help"] = "Appears when you point at the activity card. Applies immediately and is remembered after Navisworks restarts.",
        ["settings.toast.idle"] = "Idle duration",
        ["settings.toast.idle.help"] = "Hides the card when no new results arrive. Hover to keep it open. Applies from the next activity.",
        ["settings.record"] = "Record tool calls",
        ["settings.record.help"] = "Appends every tool call to mcp-calls.jsonl. Off until you turn it on. Applies immediately and is remembered after Navisworks restarts.",
        ["settings.toast.idle.seconds"] = "{seconds} seconds",
        ["settings.apply"] = "Apply",
        ["settings.close"] = "Close",
        ["settings.discard"] = "Discard unsaved idle duration?",
    };

    public static string T(string key, params (string Name, object Value)[] args)
    {
        if (key == null)
            return string.Empty;
        if (!En.TryGetValue(key, out var template))
            return key;
        if (args == null || args.Length == 0)
            return template;

        var text = template;
        foreach (var (name, value) in args)
        {
            var formatted = FormatValue(value, text, name);
            text = text.Replace("{" + name + ":n}", formatted)
                       .Replace("{" + name + "}", formatted);
        }
        return text;
    }

    private static string FormatValue(object value, string template, string name)
    {
        if (value == null)
            return string.Empty;
        if (template != null && template.Contains("{" + name + ":n}")
            && value is IConvertible c)
        {
            try { return c.ToDouble(CultureInfo.InvariantCulture).ToString("n0", CultureInfo.InvariantCulture); }
            catch { }
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
