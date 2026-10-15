// The production TCP source is linked into the offline tests. Only the Navisworks
// document lookup and UI marshal are substituted; sockets/lifecycle/IO are real.
namespace Autodesk.Navisworks.Api
{
    internal static class Application
    {
        internal static Document? ActiveDocument => null;
    }

    internal sealed class Document
    {
        internal bool IsClear => true;
        internal string Title => "";
        internal string FileName => "";
    }
}

namespace Bimwright.Nwd.Shared.Plugin
{
    internal static class NavisworksUiThreadInvoker
    {
        internal static Action<Action>? Override;
        internal static void Invoke(Action action)
        {
            if (Override is { } invoke) invoke(action);
            else action();
        }
        internal static void Invoke(Action action, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Override is { } invoke)
                Task.Run(() => invoke(() => { token.ThrowIfCancellationRequested(); action(); })).Wait(token);
            else action();
        }
    }
}
