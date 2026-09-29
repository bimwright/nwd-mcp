using System;
using System.Windows.Interop;
using Bimwright.Nwd.Shared.Config;
using Bimwright.Nwd.Shared.Transport;
using Bimwright.Nwd.Shared.Views;
using Bimwright.Nwd.Shared.Views.Toast;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// One activity card for the Navisworks session. Card clicks only dismiss:
/// there is no History window yet.
/// </summary>
internal static class NwdActivityToast
{
    private static TcpTransportServer? _server;
    private static McpToastNotifier? _notifier;
    private static NwdToastStatusWindow? _status;
    private static ClientPresence.State _presence;
    private static int _year;
    private static bool _enabled = true;
    private static IntPtr _owner;

    public static bool Enabled => _enabled;

    public static bool ShowBranding => _notifier?.ShowBranding ?? PluginSettings.LoadShowBranding();

    public static void Attach(TcpTransportServer server, int year)
    {
        _server = server;
        _year = year;
        _enabled = PluginSettings.LoadToastEnabled();
        _presence = default;
        var host = new McpToastHost();
        _notifier = new McpToastNotifier(host, () => _enabled);
        _notifier.SetInstanceInfo(Identity);
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher
                ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            _notifier.SetHostDispatcher(dispatcher);
        }
        catch
        {
        }
        server.CommandCompleted = OnCommandCompleted;
        TryAttachOwner();
    }

    public static void OnIdle()
    {
        var notifier = _notifier;
        if (notifier == null)
            return;

        notifier.SetInstanceInfo(Identity);
        TryAttachOwner();
        notifier.FlushPendingIfUsable();

        var server = _server;
        var now = DateTime.UtcNow;
        var socketOpen = server != null && server.ActiveClientCount > 0;
        if (ClientPresence.Observe(ref _presence, now, socketOpen, server?.LastCommandUtc))
            notifier.OnClientConnected(server != null ? "port " + server.Port : null);
    }

    public static void Stop()
    {
        if (_server != null)
            _server.CommandCompleted = null;
        try { _status?.Close(); } catch { }
        _status = null;
        _notifier?.Shutdown();
        _notifier = null;
        _server = null;
    }

    public static void ToggleEnabled()
    {
        ApplyEnabled(!_enabled);
    }

    public static void ApplyEnabled(bool enabled)
    {
        var persisted = PluginSettings.SaveEnableToast(enabled);
        _enabled = enabled;
        _notifier?.OnToastEnabledChanged(enabled, persisted);
    }

    public static void ToggleBranding()
    {
        if (!_enabled)
            return;
        ApplyBranding(!ShowBranding);
    }

    public static void ApplyBranding(bool show)
    {
        if (!_enabled)
            return;
        var persisted = PluginSettings.SaveShowBranding(show);
        _notifier?.SetShowBranding(show);
        if (!persisted)
            _notifier?.OnPreferenceSaveFailed();
    }

    public static void SetIdleSeconds(int seconds)
    {
        _notifier?.SetIdleSeconds(seconds);
    }

    public static void ShowStatusWindow()
    {
        if (_status != null)
        {
            _status.Activate();
            return;
        }

        _status = new NwdToastStatusWindow();
        _status.Closed += (_, _) => _status = null;
        try
        {
            if (_owner != IntPtr.Zero)
                new WindowInteropHelper(_status).Owner = _owner;
        }
        catch
        {
        }
        _status.Show();
    }

    private static string Identity => BrandAssets.ProductName + " " + _year;

    private static void OnCommandCompleted(string command, string json, bool ok, string? error)
    {
        if (!ToastCommands.ShouldToast(command))
            return;
        _notifier?.OnCompleted(command, json, ok, error, null);
    }

    private static void TryAttachOwner()
    {
        try
        {
            var window = NW.Application.Gui?.MainWindow;
            var hwnd = window != null ? window.Handle : IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
                return;
            _owner = hwnd;
            _notifier?.SetOwnerHandle(hwnd);
        }
        catch
        {
        }
    }
}
