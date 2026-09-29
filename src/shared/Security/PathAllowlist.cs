using System;
using System.IO;

namespace Bimwright.Nwd.Shared.Security;

/// <summary>
/// Directory-boundary checks for capture and toast paths. A trailing separator
/// stops a sibling folder such as capturesEvil from passing StartsWith.
/// </summary>
public static class PathAllowlist
{
    public static string CapturesDirectory =>
        Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bimwright",
            "nwd-mcp",
            "captures"));

    public static string TempDirectory => Path.GetFullPath(Path.GetTempPath());

    public static bool IsUnderRoot(string fullPath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(rootDirectory))
            return false;

        try
        {
            var full = Path.GetFullPath(fullPath);
            var root = Path.GetFullPath(rootDirectory);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())
                && !root.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                root += Path.DirectorySeparatorChar;
            }

            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsUnderTempOrCaptures(string fullPath)
    {
        return IsUnderRoot(fullPath, TempDirectory)
            || IsUnderRoot(fullPath, CapturesDirectory);
    }
}
