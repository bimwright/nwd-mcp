using System;
using System.IO;
using System.Linq;
using Bimwright.Nwd.Server;

namespace Bimwright.Nwd.Tests;

public sealed class PluginClientTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-cli-" + Guid.NewGuid().ToString("N"));
    public PluginClientTargetTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private void Write(string id, int year)
        => File.WriteAllText(Path.Combine(_dir, id + ".json"), $$"""
        { "target_id": "{{id}}", "navisworks_year": {{year}}, "process_id": {{System.Diagnostics.Process.GetCurrentProcess().Id}},
          "host_product": "Manage", "port": 1, "auth_token": "t",
          "last_heartbeat_utc": "{{DateTimeOffset.UtcNow.UtcDateTime:O}}" }
        """);

    private PluginClient Client() => new(new NwdMcpConfig { DescriptorDirectory = _dir });

    [Fact]
    public void ListsAllLiveTargets()
    {
        Write("navis-2025-1", 2025); Write("navis-2026-2", 2026);
        Assert.Equal(2, Client().ListTargets().Count);
    }

    [Fact]
    public void SwitchTargetSelectsRequestedDescriptor()
    {
        Write("navis-2025-1", 2025); Write("navis-2026-2", 2026);
        var c = Client();
        Assert.True(c.SwitchTarget("navis-2025-1"));
        Assert.Equal("navis-2025-1", c.CurrentTarget!.TargetId);
    }

    [Fact]
    public void SwitchTargetRejectsUnknownId()
        => Assert.False(Client().SwitchTarget("navis-9999-9"));
}
