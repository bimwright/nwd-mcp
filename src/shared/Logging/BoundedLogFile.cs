using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Bimwright.Nwd.Shared.Logging;

/// <summary>Only the two generated call-log groups may be rotated or pruned. <see cref="LogRetentionPolicy.KeepAll"/> skips pruning.</summary>
public static class BoundedLogFile
{
    public const long FileBytes = 5L * 1024 * 1024;
    public const long GroupBytes = 100L * 1024 * 1024;
    public const int RetentionDays = 30;
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    internal static T WithLock<T>(string path, Func<T> action, int timeoutMs = 2000)
    {
        var canonical = Path.GetFullPath(path);
        if (Path.DirectorySeparatorChar == '\\') canonical = canonical.ToUpperInvariant();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(canonical))).Replace("-", "");
        using (var mutex = new Mutex(false, (Path.DirectorySeparatorChar == '\\' ? @"Global\" : "") + "Bimwright.Nwd.File." + hash))
        {
            var acquired = false;
            try
            {
                try { acquired = mutex.WaitOne(timeoutMs); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new TimeoutException("Timed out waiting for the NWD file lock.");
                return action();
            }
            finally { if (acquired) mutex.ReleaseMutex(); }
        }
    }

    public static void Append(string path, string line)
        => Append(path, line, FileBytes, GroupBytes, DateTime.UtcNow);

    internal static void Append(string path, string line, long fileBytes, long groupBytes, DateTime utcNow)
    {
        path = Path.GetFullPath(path);
        var group = Group(path);
        var bytes = Utf8.GetBytes(line.TrimEnd('\r', '\n') + "\n");
        if (fileBytes <= 0 || groupBytes < fileBytes || bytes.LongLength > fileBytes)
            throw new InvalidDataException("Call-log record exceeds the file budget.");
        WithLock(path, () =>
        {
            RefuseLink(path);
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            if (File.Exists(path) && (new FileInfo(path).Length + bytes.LongLength > fileBytes
                || File.GetLastWriteTimeUtc(path) < utcNow.AddDays(-RetentionDays)))
                Rotate(path, utcNow);
            if (!LogRetentionPolicy.KeepAll) Prune(directory, group, path, groupBytes - bytes.LongLength, utcNow);
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                stream.Write(bytes, 0, bytes.Length);
            return true;
        });
    }

    public static void Maintain(string directory)
    {
        foreach (var name in new[] { "mcp-calls.jsonl", "nwd-mcp-calls.jsonl" })
        {
            var path = Path.GetFullPath(Path.Combine(directory, name));
            WithLock(path, () =>
            {
                RefuseLink(path);
                if (!Directory.Exists(directory)) return true;
                var now = DateTime.UtcNow;
                if (File.Exists(path) && (new FileInfo(path).Length > FileBytes
                    || File.GetLastWriteTimeUtc(path) < now.AddDays(-RetentionDays)))
                    Rotate(path, now);
                if (!LogRetentionPolicy.KeepAll) Prune(directory, Group(path), path, GroupBytes, now);
                return true;
            }, 250);
        }
    }

    internal static string[] Files(string directory, string group)
    {
        Group(Path.Combine(directory, group + ".jsonl"));
        if (!Directory.Exists(directory)) return new string[0];
        RefuseLink(Path.Combine(directory, group + ".jsonl"));
        var pattern = "^" + Regex.Escape(group) + @"(?:-\d{8}-\d{13}-[0-9a-f]{32})?\.jsonl$";
        return Directory.GetFiles(directory, group + "*.jsonl")
            .Where(p => Regex.IsMatch(Path.GetFileName(p), pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0)
            .OrderBy(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal).ToArray();
    }

    private static string Group(string path)
    {
        var name = Path.GetFileName(path);
        if (name != "mcp-calls.jsonl" && name != "nwd-mcp-calls.jsonl")
            throw new InvalidDataException("Unknown generated call-log filename.");
        return Path.GetFileNameWithoutExtension(name);
    }

    private static void Rotate(string path, DateTime now)
        => File.Move(path, Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path)
            + "-" + now.ToString("yyyyMMdd-HHmmssfffffff", System.Globalization.CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N") + ".jsonl"));

    private static void Prune(string directory, string group, string active, long budget, DateTime now)
    {
        var files = Files(directory, group);
        var total = files.Sum(p => new FileInfo(p).Length);
        foreach (var path in files)
        {
            if (string.Equals(path, active, StringComparison.OrdinalIgnoreCase)) continue;
            var info = new FileInfo(path);
            if (info.LastWriteTimeUtc >= now.AddDays(-RetentionDays) && total <= budget) continue;
            var length = info.Length;
            File.Delete(path); // A locked archive aborts append; there is no unlocked fallback.
            total -= length;
        }
        if (total > budget) throw new IOException("Call-log group budget is unavailable.");
    }

    internal static void RefuseLink(string path)
    {
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Maintenance refuses linked files.");
        for (var dir = Path.GetDirectoryName(Path.GetFullPath(path)); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(dir) && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Maintenance refuses linked directories.");
    }

}
