using System;
using System.Threading;
using System.Windows.Threading;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Marshals command execution onto the Navisworks UI thread. Navisworks API objects
/// (Document, Viewpoint, etc.) are STA-affined; the TCP transport handles requests on
/// worker threads, and touching the API there throws InvalidOperationException (0x80131509).
/// </summary>
public static class NavisworksUiThreadInvoker
{
    private static SynchronizationContext? _ui;
    private static Dispatcher? _uiDispatcher;

    public static bool HasUiContext => _ui != null || _uiDispatcher != null;

    /// <summary>
    /// Call from the Navisworks UI thread. OnLoaded, GuiCreated, and Idle all call this.
    /// SynchronizationContext.Current can still be null in OnLoaded, so a null context stays
    /// unset for a later UI callback, and the UI Dispatcher is kept as the fallback meanwhile.
    /// </summary>
    public static void Capture()
    {
        var current = SynchronizationContext.Current;
        if (current != null)
            _ui = current;
        _uiDispatcher ??= Dispatcher.CurrentDispatcher;
    }

    public static void Invoke(Action action)
    {
        if (action == null)
            return;
        var ui = _ui;
        if (ui != null)
        {
            if (SynchronizationContext.Current == ui)
                action();
            else
                ui.Send(_ => action(), null);
            return;
        }
        var dispatcher = _uiDispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }
        dispatcher.Invoke(action);
    }
}
