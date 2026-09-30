using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Bimwright.Nwd.Shared.Files;

public sealed class RecentFileEntry
{
    public int Index { get; set; }
    public string Path { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Pinned { get; set; }
    public DateTime? LastOpenedUtc { get; set; }
    public bool Exists { get; set; }
}

public sealed class RecentFileRaw
{
    public int Index { get; set; }
    public string? Path { get; set; }
    public string? DisplayName { get; set; }
    public bool Pinned { get; set; }
    public long LastOpenedFileTime { get; set; }
}

/// <summary>
/// File-menu recent list for one Navisworks Manage year.
/// The registry series is the calendar year minus 2003 (2025 → 22.0).
/// </summary>
public static class NavisworksRecentFiles
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 50;

    public static bool IsSupportedYear(int calendarYear)
        => calendarYear >= 2022 && calendarYear <= 2027;

    public static int Series(int calendarYear) => calendarYear - 2003;

    public static string RegistrySubKey(int calendarYear)
        => "Software\\Autodesk\\Navisworks Manage\\" + Series(calendarYear) + ".0\\Recent File List";

    public static int ClampLimit(int limit)
    {
        if (limit < 1) return 1;
        if (limit > MaxLimit) return MaxLimit;
        return limit;
    }

    public static IReadOnlyList<RecentFileEntry> Read(int calendarYear, int limit)
        => Read(calendarYear, limit, ReadRegistry, File.Exists);

    internal static IReadOnlyList<RecentFileEntry> Read(
        int calendarYear,
        int limit,
        Func<string, IReadOnlyList<RecentFileRaw>> source,
        Func<string, bool> exists)
    {
        var taken = new List<RecentFileEntry>();
        if (!IsSupportedYear(calendarYear))
            return taken;

        var cap = ClampLimit(limit);
        var rows = source(RegistrySubKey(calendarYear));
        var ordered = new List<RecentFileRaw>();
        if (rows != null)
        {
            foreach (var row in rows)
            {
                if (row == null || row.Path == null || row.Path.Trim().Length == 0)
                    continue;
                ordered.Add(row);
            }
        }

        ordered.Sort((a, b) => a.Index.CompareTo(b.Index));
        for (var i = 0; i < ordered.Count && taken.Count < cap; i++)
        {
            var row = ordered[i];
            var path = row.Path!.Trim();
            var name = row.DisplayName;
            if (name == null || name.Trim().Length == 0)
                name = Path.GetFileName(path);
            taken.Add(new RecentFileEntry
            {
                Index = row.Index,
                Path = path,
                DisplayName = name,
                Pinned = row.Pinned,
                LastOpenedUtc = FromFileTime(row.LastOpenedFileTime),
                Exists = exists != null && exists(path)
            });
        }

        return taken;
    }

    internal static DateTime? FromFileTime(long fileTime)
    {
        if (fileTime <= 0)
            return null;
        try
        {
            var utc = DateTime.FromFileTimeUtc(fileTime);
            if (utc.Year < 2000 || utc.Year > 2100)
                return null;
            return utc;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IReadOnlyList<RecentFileRaw> ReadRegistry(string subKey)
    {
        var rows = new List<RecentFileRaw>();
#pragma warning disable CA1416
        using (var key = Registry.CurrentUser.OpenSubKey(subKey))
        {
            if (key == null)
                return rows;
            foreach (var name in key.GetSubKeyNames())
            {
                int index;
                if (!int.TryParse(name, out index))
                    continue;
                using (var child = key.OpenSubKey(name))
                {
                    if (child == null)
                        continue;
                    rows.Add(new RecentFileRaw
                    {
                        Index = index,
                        Path = child.GetValue("Path") as string,
                        DisplayName = child.GetValue("DisplayName") as string,
                        Pinned = ToInt32(child.GetValue("Pinned")) != 0,
                        LastOpenedFileTime = ToInt64(child.GetValue("LastOpened"))
                    });
                }
            }
        }
#pragma warning restore CA1416

        return rows;
    }

    private static int ToInt32(object? value)
    {
        if (value is int i) return i;
        if (value == null) return 0;
        try { return Convert.ToInt32(value); }
        catch (Exception) { return 0; }
    }

    private static long ToInt64(object? value)
    {
        if (value is long l) return l;
        if (value is int i) return i;
        if (value == null) return 0;
        try { return Convert.ToInt64(value); }
        catch (Exception) { return 0; }
    }
}
