using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Views.Update;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Update;

/// <summary>
/// Coordinates the non-blocking GitHub latest-release check and shows UpdateNoticeWindow at most once per
/// Navisworks session. Startup uses a 24h cooldown; opening History or Status uses a 30m cooldown.
/// </summary>
internal static class UpdateNoticeCoordinator
{
    private static string? _statePath;
    private static string? _currentVersion;
    private static int _inFlight;
    private static int _shown;
    private static UpdateNoticeWindow? _window;

    public static void Start(string descriptorDir, Assembly pluginAssembly)
    {
        _statePath = Path.Combine(descriptorDir, "update-check.json");
        _currentVersion = UpdateChecker.VersionOf(pluginAssembly);
        Interlocked.Exchange(ref _shown, 0);
        TriggerCheck(UpdateChecker.CheckInterval);
    }

    /// <summary>Re-checks with a 30-minute cooldown when the user opens History or Status.</summary>
    public static void TriggerFromUi()
    {
        TriggerCheck(UpdateChecker.InteractiveCheckInterval);
    }

    public static void Stop()
    {
        var window = _window;
        _window = null;
        if (window == null) return;
        try
        {
            if (window.Dispatcher.CheckAccess()) window.Close();
            else window.Dispatcher.BeginInvoke(new Action(() => { try { window.Close(); } catch { } }));
        }
        catch { }
    }

    private static void TriggerCheck(TimeSpan interval)
    {
        var statePath = _statePath;
        var currentVersion = _currentVersion;
        if (string.IsNullOrEmpty(statePath) || string.IsNullOrEmpty(currentVersion)) return;
        if (UpdateChecker.IsDisabled(Environment.GetEnvironmentVariable)) return;
        if (Volatile.Read(ref _shown) != 0) return;
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var info = await UpdateChecker.CheckAsync(currentVersion!, statePath!, checkInterval: interval).ConfigureAwait(false);
                if (info == null || info.IsSkipped) return;
                if (Interlocked.CompareExchange(ref _shown, 1, 0) != 0) return;
                ShowOnUiThread(info, statePath!);
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _inFlight, 0);
            }
        });
    }

    private static void ShowOnUiThread(UpdateInfo info, string statePath)
    {
        void Show()
        {
            try
            {
                var w = new UpdateNoticeWindow(info, statePath);
                try
                {
                    var hwnd = NW.Application.Gui?.MainWindow?.Handle ?? IntPtr.Zero;
                    if (hwnd != IntPtr.Zero)
                        new WindowInteropHelper(w).Owner = hwnd;
                    else
                        w.Topmost = true;
                }
                catch
                {
                    w.Topmost = true;
                }
                w.Closed += (_, __) => { if (ReferenceEquals(_window, w)) _window = null; };
                _window = w;
                w.Show();
            }
            catch { }
        }

        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                dispatcher.BeginInvoke(new Action(Show));
                return;
            }
            if (NavisworksUiThreadInvoker.HasUiContext)
                NavisworksUiThreadInvoker.Invoke(Show);
        }
        catch { }
    }
}
