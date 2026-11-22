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
        ["settings.toast.idle.saveFailed"] = "Toast duration could not be saved. The previous duration is still active. Try Apply again.",
        ["toast.connected.title"] = "Agent connected",
        ["settings.toast.snooze"] = "Pause for today",
        ["settings.toast.snooze.help"] = "Hides activity notifications in every Navisworks on this computer until midnight. Turn off to show them again now.",
        ["toast.snooze.today"] = "Pause for today",
        ["toast.snooze.today.help"] = "Hide activity notifications in every Navisworks until midnight. Turn them back on in Status or with the Toasts button.",
        ["toast.snooze.resume"] = "Resume",
        ["toast.snooze.resume.help"] = "Notifications are paused in every Navisworks until midnight. Click to show them again now.",
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
        ["toast.generic.fields"] = "Result: {count:n} fields",
        ["toast.items.hidden"] = "Hidden {count:n} items",
        ["toast.items.shown"] = "Shown {count:n} items",
        ["toast.items.selected"] = "Selected {count:n} items",
        ["toast.items.matched"] = "Matched {count:n} items",
        ["toast.generic.fileFallback"] = "file",
        ["toast.activity.success"] = "Success",
        ["toast.activity.failed"] = "Failed",
        ["toast.activity.capture"] = "Capture",
        ["toast.activity.open_image"] = "Open image",
        ["toast.activity.recent"] = "Activity · {count:n} calls",
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
        ["settings.toast.horizontal"] = "Horizontal alignment",
        ["settings.toast.vertical"] = "Vertical alignment",
        ["settings.toast.left"] = "Left",
        ["settings.toast.right"] = "Right",
        ["settings.toast.top"] = "Top",
        ["settings.toast.bottom"] = "Bottom",
        ["settings.toast.position.help"] = "Applies and saves immediately. Changing a corner clears the saved drag position.",
        ["settings.toast.drag"] = "Allow dragging the card",
        ["settings.toast.drag.help"] = "Drag the title row to move the card. Turning this off keeps the saved position; turning it on restores it.",
        ["settings.toast.reset"] = "Reset position",
        ["settings.toast.idle"] = "Idle duration",
        ["settings.toast.idle.help"] = "Hides the card when no new results arrive. Hover to keep it open. Applies from the next activity.",
        ["settings.record"] = "Record tool calls",
        ["settings.record.help"] = "Appends every tool call to mcp-calls.jsonl. Off until you turn it on. Applies immediately and is remembered after Navisworks restarts.",
        ["settings.toast.idle.seconds"] = "{seconds} seconds",
        ["settings.apply"] = "Apply",
        ["settings.close"] = "Close",
        ["settings.discard"] = "Discard unsaved idle duration?",
        ["toast.category.query"] = "MCP · Query",
        ["toast.category.modified"] = "MCP · Modified",
        ["toast.category.script"] = "MCP · Script",
        ["toast.category.export"] = "MCP · Export",
        ["toast.category.snapshot"] = "MCP · Snapshot",
        ["toast.category.connected"] = "MCP · Connected",
        ["toast.category.failed"] = "MCP · Failed",
        ["toast.activity.header"] = "MCP · Activity",
        ["toast.activity.latest"] = "Latest",
        ["toast.activity.counts.tooltip"] = "Success / failed / images",
        ["toast.survey.partial"] = "Survey: coverage incomplete",
        ["toast.survey.complete"] = "Survey: supported checks complete",
        ["toast.survey.review"] = "Review scope and blind spots before changes",
        ["toast.capture.savedPx"] = "Saved {fileName} · {pixelSize}px {format}",
        ["toast.capture.viewId"] = "View id {viewId}",
        ["toast.currentView.active"] = "Active view",
        ["toast.currentView.scale"] = "Scale 1:{scale}",
        ["toast.rooms.none"] = "No rooms in model",
        ["toast.rooms.found"] = "Rooms found: {count:n}",
        ["toast.rooms.complete"] = "Room query complete",
        ["toast.rooms.placedStats"] = "Placed {placed:n}, unplaced {unplaced:n}",
        ["toast.filters.allLevelsPhases"] = "All levels · all phases",
        ["toast.filters.allLevels"] = "All levels",
        ["toast.filters.level"] = "Level: {level}",
        ["toast.filters.allPhases"] = "All phases",
        ["toast.filters.phase"] = "Phase: {phase}",
        ["toast.filters.status"] = "Status: {status}",
        ["toast.sheets.none"] = "No sheets in project",
        ["toast.sheets.found"] = "Drawing sheets: {count:n}",
        ["toast.sheets.ready"] = "Sheet list ready",
        ["toast.sheets.first"] = "First: {number} {name}",
        ["toast.sheets.more"] = "(+{count:n} more)",
        ["toast.sheets.fallback"] = "Sheets",
        ["toast.worksets.notWorkshared"] = "Model is not workshared",
        ["toast.worksets.none"] = "No worksets to list",
        ["toast.worksets.found"] = "Worksets: {count:n}",
        ["toast.worksets.complete"] = "Workset query complete",
        ["toast.worksets.active"] = "Active: {name}",
        ["toast.filter.matched"] = "Matched {count:n} {category}",
        ["toast.filter.elementsFallback"] = "elements",
        ["toast.filter.complete"] = "Element filter complete",
        ["toast.selected.count"] = "Elements selected: {count:n}",
        ["toast.selected.done"] = "Selection read",
        ["history.title"] = "{identity} · History",
        ["history.search"] = "Search",
        ["history.all"] = "All",
        ["history.time"] = "Time",
        ["history.tool"] = "Tool",
        ["history.summary"] = "Outcome",
        ["history.duration"] = "Duration (ms)",
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
