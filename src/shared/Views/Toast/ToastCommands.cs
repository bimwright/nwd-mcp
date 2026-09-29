using System;

namespace Bimwright.Nwd.Shared.Views.Toast;

/// <summary>Which plug-in commands are allowed onto the activity card.</summary>
public static class ToastCommands
{
    public static bool ShouldToast(string? command)
    {
        return !string.IsNullOrWhiteSpace(command)
            && !string.Equals(command, "health_check", StringComparison.Ordinal);
    }
}
