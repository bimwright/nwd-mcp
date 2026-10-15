using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Bimwright.Nwd.Shared.Logging;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class TargetRegistry
{
    private readonly string _dir;
    private readonly TimeSpan _maxAge;
    public TargetRegistry(string directory, int maxAgeSeconds = 120)
    { _dir = directory; _maxAge = TimeSpan.FromSeconds(maxAgeSeconds); }

    public IReadOnlyList<TargetDescriptor> List()
    {
        try { return BoundedLogFile.WithLock(LockPath(_dir), ListUnderLock, 250); }
        catch (TimeoutException) { return Array.Empty<TargetDescriptor>(); }
    }

    internal static string LockPath(string directory) => Path.Combine(directory, ".target-registry");

    private IReadOnlyList<TargetDescriptor> ListUnderLock()
    {
        if (!Directory.Exists(_dir)) return Array.Empty<TargetDescriptor>();
        var now = DateTimeOffset.UtcNow;
        var live = new List<TargetDescriptor>();
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            TargetDescriptor? d;
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 64 * 1024) continue;
                using var reader = new StreamReader(stream);
                d = JsonConvert.DeserializeObject<TargetDescriptor>(reader.ReadToEnd());
            }
            catch { continue; }
            if (d is null) continue;
            if (!string.Equals(d.HostProduct, "Manage", StringComparison.OrdinalIgnoreCase)) continue;
            if (d.NavisworksYear is < 2022 or > 2027) continue;
            if (now - d.LastHeartbeatUtc > _maxAge) continue;
            if (!IsProcessAlive(d.ProcessId)) continue;
            live.Add(d);
        }
        return live.OrderByDescending(x => x.LastHeartbeatUtc).ToList();
    }

    private static bool IsProcessAlive(int pid)
    {
        if (pid <= 0) return false;
        try { System.Diagnostics.Process.GetProcessById(pid); return true; }
        catch { return false; }
    }
}
