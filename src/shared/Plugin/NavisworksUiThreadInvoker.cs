using System;
using System.Threading;

namespace Bimwright.Nwd.Shared.Plugin;

public static class NavisworksUiThreadInvoker
{
    private static SynchronizationContext? _ui;

    public static bool HasUiContext => _ui != null;

    /// <summary>
    /// Call from the Navisworks UI thread. OnLoaded, GuiCreated, and Idle all call this.
    /// A null context stays unset so a later UI callback can try again.
    /// </summary>
    public static void Capture()
    {
        var current = SynchronizationContext.Current;
        if (current != null)
            _ui = current;
    }

    public static void Invoke(Action action)
    {
        if (action == null)
            return;
        var ui = _ui;
        if (ui == null || SynchronizationContext.Current == ui)
        {
            action();
            return;
        }
        ui.Send(_ => action(), null);
    }
}
