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
    public void IgnoresStaleHeartbeat()
    {
        WriteDescriptor("a.json", 2026, AliveProcessId(), "Manage", DateTimeOffset.UtcNow.AddSeconds(-200));
        Assert.Empty(new TargetRegistry(_dir).List());
    }

    [Fact]
    public void IgnoresDeadProcess()
    {
        WriteDescriptor("a.json", 2026, 0x7FFFFFFE, "Manage", DateTimeOffset.UtcNow); // pid that is not running
        Assert.Empty(new TargetRegistry(_dir).List());
    }

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
