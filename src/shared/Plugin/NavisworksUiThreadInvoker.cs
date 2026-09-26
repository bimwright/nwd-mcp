using System;
using System.Windows.Threading;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Marshals command execution onto the Navisworks UI thread.
/// Navisworks API objects (Document, Viewpoint, etc.) are STA-affined
/// and must be accessed from the main UI thread. The TCP transport
/// handles requests on worker threads, so all handler execution is
/// funneled through this invoker.
/// </summary>
public static class NavisworksUiThreadInvoker
{
    private static Dispatcher? _uiDispatcher;

    /// <summary>
    /// Captures the UI dispatcher. Called from the plugin's OnLoaded
    /// which runs on the main thread.
    /// </summary>
    public static void SetUiContext(Dispatcher? dispatcher)
    {
        _uiDispatcher = dispatcher;
    }

    /// <summary>
    /// Runs the action on the UI thread. If already on the UI thread,
    /// runs inline. Otherwise blocks until the UI thread processes it.
    /// </summary>
    public static void Invoke(Action action)
    {
        if (action == null) return;

        if (_uiDispatcher == null)
        {
            action();
            return;
        }

        if (_uiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        _uiDispatcher.Invoke(action);
    }
}