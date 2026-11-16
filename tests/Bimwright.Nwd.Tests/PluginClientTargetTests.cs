using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class PluginClientTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-cli-" + Guid.NewGuid().ToString("N"));
    public PluginClientTargetTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    // Distinct fake PIDs share the live test process, so identity comes from this table.
    private readonly Dictionary<int, ProcessProbe> _processes = new();

    private void Write(int pid, int year, int port = 1)
    {
        var start = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc).AddMinutes(pid);
        _processes[pid] = new ProcessProbe(ProcessState.Alive, start);
        File.WriteAllText(Path.Combine(_dir, $"navis-{year}-{pid}.json"), $$"""
        { "schema_version": 3, "host_app": "navisworks", "host_year": {{year}}, "pid": {{pid}},
          "process_start_utc": "{{start:O}}", "transport": "tcp",
          "target_id": "navis-{{year}}-{{pid}}", "navisworks_year": {{year}}, "process_id": {{pid}},
          "host_product": "Manage", "port": {{port}}, "auth_token": "token",
          "last_heartbeat_utc": "{{DateTimeOffset.UtcNow.UtcDateTime:O}}" }
        """);
    }

    private PluginClient Client(string? target = null) => new(
        new NwdMcpConfig { DescriptorDirectory = _dir, TargetId = target, TimeoutMs = 3000 },
        pid => _processes.TryGetValue(pid, out var p) ? p : new ProcessProbe(ProcessState.Gone, null),
        _ => null);

    [Fact]
    public void ListsAllLiveTargets()
    {
        Write(1, 2025); Write(2, 2026);
        Assert.Equal(2, Client().ListTargets().Count);
    }

    [Fact]
    public void SwitchTargetSelectsRequestedDescriptor()
    {
        Write(1, 2025); Write(2, 2026);
        var c = Client();
        Assert.True((bool)c.SwitchTarget("navis-2025-1")["ok"]!);
        var state = c.CurrentTargetState();
        Assert.Equal("navis-2025-1", (string?)state["bound_target_id"]);
        Assert.True((bool)state["identity_verified"]!);
    }

    [Fact]
    public void SwitchTargetRejectsUnknownIdWithTargetPayload()
    {
        var ex = Assert.Throws<NwdGatewayException>(() => Client().SwitchTarget("navis-2025-9"));
        Assert.Equal("NO_TARGET", ex.Code);
        Assert.False((bool)ex.Target!["sent"]!);
    }

    [Fact]
    public void InvalidStartupSelectorStopsTheGateway()
        => Assert.Throws<ArgumentException>(() => Client("R25"));

    [Fact]
    public async Task TwoInstancesWithoutAChoiceRefuseTheCommandBeforeConnecting()
    {
        Write(1, 2025); Write(2, 2025);
        var ex = await Assert.ThrowsAsync<NwdGatewayException>(() => Client().SendAsync("health_check", new JObject(), CancellationToken.None));
        Assert.Equal("AMBIGUOUS_TARGET", ex.Code);
    }

    [Fact]
    public async Task ConnectionClosedAfterSendIsInterruptedWithUnknownOutcome() // BW-TARGET-001 6.7
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Write(7, 2025, ((IPEndPoint)listener.LocalEndpoint).Port);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            await reader.ReadLineAsync(); // receive the command, then drop the connection
        });

        var ex = await Assert.ThrowsAsync<NwdGatewayException>(() => Client().SendAsync("get_document_info", new JObject(), CancellationToken.None));
        await server;
        Assert.Equal("TARGET_INTERRUPTED", ex.Code);
        Assert.True((bool)ex.Target!["sent"]!);
        Assert.Equal("unknown", (string?)ex.Target["outcome"]);
    }

    [Fact]
    public async Task TargetPayloadReachesTheAgentThroughTheToolError()
    {
        Write(1, 2025); Write(2, 2026);
        var client = Client();
        using (TargetCallContext.Begin(client.Generation))
        {
            var text = await new SelectionTools(client).GetCurrentSelection(CancellationToken.None);
            var result = TargetErrorPayload.Apply(new ModelContextProtocol.Protocol.CallToolResult
            {
                Content = new List<ModelContextProtocol.Protocol.ContentBlock> { new ModelContextProtocol.Protocol.TextContentBlock { Text = text } }
            });
            var error = JObject.Parse(((ModelContextProtocol.Protocol.TextContentBlock)result.Content[0]).Text!)["error"]!;
            Assert.Equal("AMBIGUOUS_TARGET", (string?)error["code"]);
            Assert.Equal(2, ((JArray)error["target"]!["candidates"]!).Count);
            Assert.Equal("nwd_list_available_targets", (string?)error["target"]!["recommended_next_tool"]);
        }
    }

    [Fact]
    public async Task SendCodeWrapperUsesCodeParameterOnWire()
    {
        using var plugin = new Helpers.FakePlugin(); // confirmed document, so the write is sent
        await new CodeTools(plugin.Client).SendCode("System.Console.WriteLine(1);", CancellationToken.None);

        var received = plugin.Single();
        Assert.Equal("send_code", received.Command);
        Assert.Equal("System.Console.WriteLine(1);", (string?)received.Params["code"]);
        Assert.Null(received.Params["source"]);
    }
}
