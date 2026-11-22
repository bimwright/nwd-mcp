using System;

namespace Bimwright.Nwd.Shared.Logging;

/// <summary>
/// BIMWRIGHT_KEEP_ALL_LOGS=1|true|yes|on keeps every call log and report: automatic pruning is skipped,
/// rotation still archives. It never turns a recorder on or changes what is recorded.
/// </summary>
public static class LogRetentionPolicy
{
    public const string EnvironmentVariable = "BIMWRIGHT_KEEP_ALL_LOGS";
    public const string StartupNotice = "nwd-mcp: BIMWRIGHT_KEEP_ALL_LOGS is on; call logs and reports are kept (automatic deletion is off).";

    /// <summary>Test hook. Null reads the environment variable.</summary>
    internal static bool? Override { get; set; }

    public static bool KeepAll => Override ?? IsOn(Environment.GetEnvironmentVariable(EnvironmentVariable));

    internal static bool IsOn(string? raw)
    {
        if (raw == null) return false;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                return true;
            default:
                return false;
        }
    }
}
