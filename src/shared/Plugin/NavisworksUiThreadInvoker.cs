using System;

namespace Bimwright.Nwd.Shared.Plugin;

public static class NavisworksUiThreadInvoker
{
    public static void Invoke(Action action)
    {
        if (action == null) return;
        // In a real Navisworks environment, marshal to the UI thread.
        // For testing/scaffold, invoke synchronously.
        action();
    }
}
