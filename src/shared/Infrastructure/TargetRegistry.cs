using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Bimwright.Nwd.Shared.Logging;

namespace Bimwright.Nwd.Shared.Infrastructure;

/// <summary>
/// Reads per-instance descriptors (BW-TARGET-001 §5.3, §5.6, liveness L1). A descriptor is live when
/// its process exists and, if the descriptor records <c>process_start_utc</c>, the observed start time
/// is within one second of it. Heartbeat age is never used. Only descriptors proven dead are deleted.
/// </summary>
public sealed class TargetRegistry
{
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(1);
    private readonly string _dir;
    private readonly Func<int, ProcessProbe> _probe;

    public TargetRegistry(string directory) : this(directory, null) { }

    /// <param name="probe">Test seam: reads a process by PID. Default uses the OS.</param>
    public TargetRegistry(string directory, Func<int, ProcessProbe>? probe)
    { _dir = directory; _probe = probe ?? ProcessProbe.Read; }

    public IReadOnlyList<TargetDescriptor> List()
    {
        try { return BoundedLogFile.WithLock(LockPath(_dir), ListUnderLock, 250); }
        catch (TimeoutException) { return Array.Empty<TargetDescriptor>(); }
    }

    internal static string LockPath(string directory) => Path.Combine(directory, ".target-registry");

    private IReadOnlyList<TargetDescriptor> ListUnderLock()
    {
        if (!Directory.Exists(_dir)) return Array.Empty<TargetDescriptor>();
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
            catch { continue; } // Unparseable: not live, never deleted.
            if (d is null) continue;
            if (!IsNavisworksManage(d)) continue;
            if (d.Year is < 2022 or > 2027) continue;
            var pid = d.ProcessIdNormalized;
            if (pid <= 0 || string.IsNullOrEmpty(d.AuthToken) || d.Port <= 0) continue;

            var process = _probe(pid);
            if (process.State == ProcessState.Gone || (process.StartUtc is { } observed && d.ProcessStartUtc is { } recorded
                && (observed - recorded.ToUniversalTime()).Duration() > StartTolerance))
            {
                TryDeleteProvenDead(file, d.AuthToken);
                continue;
            }
            d.ObservedStartUtc = process.StartUtc;
            d.IdentityVerified = process.StartUtc != null && d.ProcessStartUtc != null;
            live.Add(d);
        }
        return Sort(live);
    }

    /// <summary>BW-TARGET-001 §6.3: newest year, then newest process start (missing last), then highest PID.</summary>
    public static IReadOnlyList<TargetDescriptor> Sort(IEnumerable<TargetDescriptor> targets)
        => targets
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.ProcessStartUtc.HasValue)
            .ThenByDescending(t => t.ProcessStartUtc)
            .ThenByDescending(t => t.ProcessIdNormalized)
            .ToList();

    private static bool IsNavisworksManage(TargetDescriptor d)
        => string.Equals(d.HostApp, "navisworks", StringComparison.OrdinalIgnoreCase)
            || (d.HostApp == null && string.Equals(d.HostProduct, "Manage", StringComparison.OrdinalIgnoreCase));

    private static void TryDeleteProvenDead(string file, string authToken)
    {
        try
        {
            // Re-read under the registry lock: a restarted host may already have replaced it.
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                if (JsonConvert.DeserializeObject<TargetDescriptor>(reader.ReadToEnd())?.AuthToken != authToken) return;
            File.Delete(file);
        }
        catch { }
    }
}

public enum ProcessState { Alive, Gone, Unknown }

/// <summary>Observed process state. <see cref="StartUtc"/> is null when it cannot be read.</summary>
public readonly struct ProcessProbe
{
    public ProcessProbe(ProcessState state, DateTime? startUtc) { State = state; StartUtc = startUtc; }
    public ProcessState State { get; }
    public DateTime? StartUtc { get; }

    public static ProcessProbe Read(int pid)
    {
        if (pid <= 0) return new ProcessProbe(ProcessState.Gone, null);
        System.Diagnostics.Process process;
        try { process = System.Diagnostics.Process.GetProcessById(pid); }
        catch (ArgumentException) { return new ProcessProbe(ProcessState.Gone, null); }
        catch { return new ProcessProbe(ProcessState.Unknown, null); }
        using (process)
        {
            try { if (process.HasExited) return new ProcessProbe(ProcessState.Gone, null); }
            catch { return new ProcessProbe(ProcessState.Unknown, null); } // Access denied: treat as alive.
            try { return new ProcessProbe(ProcessState.Alive, process.StartTime.ToUniversalTime()); }
            catch { return new ProcessProbe(ProcessState.Alive, null); }
        }
    }
}
