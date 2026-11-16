using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Detects a modal dialog in Navisworks without the UI thread or the Navisworks API (ipt-mcp ModalDialogProbe parity):
/// it runs on transport threads while the UI thread may be inside the dialog's message loop. A modal dialog disables
/// its owner, so a disabled main frame plus a visible, enabled top-level window on the UI thread is a modal dialog;
/// its caption tells the agent what is waiting.
/// </summary>
public static class ModalDialogProbe
{
    private static IntPtr _mainWindow;

    /// <summary>Main frame HWND. Set on the UI thread (GuiCreated, Idle) from Application.Gui.MainWindow.</summary>
    public static IntPtr MainWindow
    {
        get => _mainWindow;
        set { if (value != IntPtr.Zero) _mainWindow = value; }
    }

    public static JObject Probe()
    {
        var result = new JObject { ["open"] = false };
        var main = _mainWindow;
        if (main == IntPtr.Zero || !IsWindow(main)) return result;
        try
        {
            if (IsWindowEnabled(main)) return result;
            var uiThread = GetWindowThreadProcessId(main, out var pid);
            var dialogs = new List<(string Title, string Class)>();
            EnumWindows((h, _) =>
            {
                if (h == main || !IsWindowVisible(h) || !IsWindowEnabled(h)) return true;
                if (GetWindowThreadProcessId(h, out var wpid) != uiThread || wpid != pid) return true;
                dialogs.Add((Text(h), Class(h)));
                return dialogs.Count < 5;
            }, IntPtr.Zero);
            if (dialogs.Count == 0) return result;   // disabled with no window to answer: not a dialog the user sees

            result["open"] = true;
            // Prefer a standard dialog (#32770) caption; else the first enabled window.
            var pick = dialogs.Find(d => d.Class == "#32770");
            if (pick.Title is null) pick = dialogs[0];
            result["title"] = pick.Title ?? "";
            if (dialogs.Count > 1)
            {
                var all = new JArray();
                foreach (var d in dialogs) all.Add(d.Title);
                result["windows"] = all;
            }
        }
        catch
        {
            // advisory
        }
        return result;
    }

    /// <summary>True when a modal dialog owns the Navisworks UI; <paramref name="title"/> is its caption.</summary>
    public static bool IsOpen(out string? title)
    {
        var probe = Probe();
        title = (string?)probe["title"];
        return probe["open"]?.Value<bool>() == true;
    }

    private static string Text(IntPtr h)
    {
        var len = GetWindowTextLength(h);
        var sb = new StringBuilder(Math.Max(len + 1, 2));
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string Class(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int count);
}
