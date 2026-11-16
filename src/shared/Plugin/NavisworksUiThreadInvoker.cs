using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Bimwright.Nwd.Shared.Infrastructure;

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

    public static void Invoke(Action action) => Invoke(action, CancellationToken.None);

    /// <summary>
    /// Family contract FC-BUSY-01..04: runs <paramref name="action"/> on the UI thread if the UI thread starts it by
    /// <paramref name="startByUtc"/>. Returns false when it did not: the queued work is abandoned and never runs later
    /// (HOST_BUSY). Once started it is waited for until <paramref name="cancellationToken"/>; a cancellation then throws
    /// OperationCanceledException and the outcome is unknown (TIMEOUT). Exceptions from the action are rethrown.
    /// </summary>
    public static bool TryInvoke(Action action, DateTime startByUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ui = _ui;
        var dispatcher = _uiDispatcher;
        if ((ui != null && SynchronizationContext.Current == ui)
            || (ui == null && (dispatcher == null || dispatcher.CheckAccess())))
        {
            action();
            return true;
        }
        if (ui != null) return StartGate.Run(run => ui.Post(_ => run(), null), action, startByUtc, cancellationToken);
        return StartGate.Run(run => dispatcher!.BeginInvoke(run), action, startByUtc, cancellationToken);
    }

    public static void Invoke(Action action, CancellationToken cancellationToken)
    {
        if (action == null)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        var ui = _ui;
        var dispatcher = _uiDispatcher;
        if ((ui != null && SynchronizationContext.Current == ui)
            || (ui == null && (dispatcher == null || dispatcher.CheckAccess())))
        {
            action();
            return;
        }
        // Unload can run on the UI thread while workers are queued here. Canceling
        // their wait must not require that same UI thread to process a Send/Invoke.
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Run()
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                completion.TrySetResult(true);
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        }
        if (ui != null) ui.Post(_ => Run(), null);
        else dispatcher!.BeginInvoke((Action)Run);
        using (cancellationToken.Register(() => completion.TrySetCanceled()))
            completion.Task.GetAwaiter().GetResult();
    }
}
