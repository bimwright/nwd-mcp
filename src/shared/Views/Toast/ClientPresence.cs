using System;

namespace Bimwright.Nwd.Shared.Views.Toast;

/// <summary>
/// Agent presence for a connect-per-command TCP client. Present while a socket
/// is open or the last command is inside <see cref="GapSeconds"/>. A poll does
/// not extend that window. Observe returns true once, on the rising edge of a burst.
/// </summary>
public static class ClientPresence
{
    public const int GapSeconds = 30;

    public struct State
    {
        public DateTime LastCommandUtc;
        public bool WasPresent;
    }

    public static bool Observe(ref State state, DateTime nowUtc, bool socketOpen, DateTime? lastCommandUtc)
    {
        var gap = TimeSpan.FromSeconds(GapSeconds);
        if (lastCommandUtc.HasValue)
            state.LastCommandUtc = lastCommandUtc.Value;

        var present = socketOpen
            || (lastCommandUtc.HasValue && nowUtc - lastCommandUtc.Value < gap);
        var rising = present && !state.WasPresent;
        state.WasPresent = present;
        return rising;
    }
}
