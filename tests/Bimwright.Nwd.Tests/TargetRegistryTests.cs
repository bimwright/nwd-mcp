using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Bimwright.Nwd.Shared.Infrastructure;

namespace Bimwright.Nwd.Tests;

public sealed class TargetRegistryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-reg-" + Guid.NewGuid().ToString("N"));
    public TargetRegistryTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private void WriteDescriptor(string name, int year, int pid, string host, DateTimeOffset heartbeat)
        => File.WriteAllText(Path.Combine(_dir, name), $$"""
        {
          "target_id": "navis-{{year}}-{{pid}}",
          "navisworks_year": {{year}},
          "process_id": {{pid}},
          "host_product": "{{host}}",
          "port": 48500,
          "auth_token": "secret-token",
          "document_title": "sample.nwd",
          "last_heartbeat_utc": "{{heartbeat.UtcDateTime:O}}"
        }
        """);

    [Fact]
    public void IgnoresNonManageHost()
    {
        WriteDescriptor("a.json", 2026, AliveProcessId(), "Freedom", DateTimeOffset.UtcNow);
        Assert.Empty(new TargetRegistry(_dir).List());
    }

    [Fact]
    public void StaleHeartbeatIsStillListed() // BW-TARGET-001 5.6: heartbeat is display only
    {
        WriteDescriptor("a.json", 2026, AliveProcessId(), "Manage", DateTimeOffset.UtcNow.AddHours(-3));
        Assert.Single(new TargetRegistry(_dir).List());
    }

    [Fact]
    public void DeadProcessIsDroppedAndItsDescriptorDeleted()
    {
        WriteDescriptor("a.json", 2026, 0x7FFFFFFE, "Manage", DateTimeOffset.UtcNow); // pid that is not running
        Assert.Empty(new TargetRegistry(_dir).List());
        Assert.False(File.Exists(Path.Combine(_dir, "a.json")));
    }

    [Fact]
    public void ReusedPidIsDroppedAndDeleted() // FC-TGT-04
    {
        var recorded = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        WriteV3("b.json", 2025, 4242, recorded);
        var registry = new TargetRegistry(_dir, _ => new ProcessProbe(ProcessState.Alive, recorded.AddMinutes(30)));
        Assert.Empty(registry.List());
        Assert.False(File.Exists(Path.Combine(_dir, "b.json")));
    }

    [Fact]
    public void MatchingStartTimeIsVerifiedAndAccessDeniedIsKeptUnverified()
    {
        var recorded = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        WriteV3("b.json", 2025, 4242, recorded);
        var verified = new TargetRegistry(_dir, _ => new ProcessProbe(ProcessState.Alive, recorded.AddMilliseconds(400))).List();
        Assert.True(Assert.Single(verified).IdentityVerified);
        Assert.Equal(2025, verified[0].Year);
        Assert.Equal(4242, verified[0].ProcessIdNormalized);

        var unknown = new TargetRegistry(_dir, _ => new ProcessProbe(ProcessState.Unknown, null)).List();
        Assert.False(Assert.Single(unknown).IdentityVerified);
        Assert.True(File.Exists(Path.Combine(_dir, "b.json")));
    }

    [Fact]
    public void UnparseableDescriptorIsNeverDeleted()
    {
        File.WriteAllText(Path.Combine(_dir, "broken.json"), "{ not json");
        Assert.Empty(new TargetRegistry(_dir).List());
        Assert.True(File.Exists(Path.Combine(_dir, "broken.json")));
    }

    [Fact]
    public void SortsByYearThenNewestProcessStart() // BW-TARGET-001 6.3
    {
        var t = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
        WriteV3("1.json", 2025, 1, t);
        WriteV3("2.json", 2025, 2, t.AddHours(1));
        WriteV3("3.json", 2026, 3, t);
        var list = new TargetRegistry(_dir, pid => new ProcessProbe(ProcessState.Alive, pid == 2 ? t.AddHours(1) : t)).List();
        Assert.Equal(new[] { 3, 2, 1 }, list.Select(d => d.ProcessIdNormalized).ToArray());
    }

    private void WriteV3(string name, int year, int pid, DateTime start)
        => File.WriteAllText(Path.Combine(_dir, name), $$"""
        { "schema_version": 3, "host_app": "navisworks", "host_year": {{year}}, "pid": {{pid}},
          "process_start_utc": "{{start:O}}", "transport": "tcp", "target_id": "navis-{{year}}-{{pid}}",
          "navisworks_year": {{year}}, "process_id": {{pid}}, "host_product": "Manage", "port": 48500,
          "auth_token": "secret-token", "last_heartbeat_utc": "{{start:O}}" }
        """);

    [Fact]
    public void ReturnsLiveManageTarget()
    {
        WriteDescriptor("a.json", 2026, AliveProcessId(), "Manage", DateTimeOffset.UtcNow);
        var list = new TargetRegistry(_dir).List();
        Assert.Single(list);
        Assert.Equal(2026, list[0].NavisworksYear);
    }

    private static int AliveProcessId() => System.Diagnostics.Process.GetCurrentProcess().Id;
}
