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

    [Fact]
    public async Task SendCodeWrapperUsesCodeParameterOnWire()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var targetId = "navis-2026-" + System.Diagnostics.Process.GetCurrentProcess().Id;
        File.WriteAllText(Path.Combine(_dir, targetId + ".json"), $$"""
        { "target_id": "{{targetId}}", "navisworks_year": 2026, "process_id": {{System.Diagnostics.Process.GetCurrentProcess().Id}},
          "host_product": "Manage", "port": {{port}}, "auth_token": "token",
          "last_heartbeat_utc": "{{DateTimeOffset.UtcNow.UtcDateTime:O}}" }
        """);

        NwdCommandEnvelope? received = null;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            received = JsonConvert.DeserializeObject<NwdCommandEnvelope>((await reader.ReadLineAsync())!);
            await writer.WriteLineAsync(JsonConvert.SerializeObject(NwdCommandResult.Success(received!.Id, new JObject { ["ok"] = true }, new NwdResponseMeta())));
        });

        var tools = new CodeTools(new PluginClient(new NwdMcpConfig { DescriptorDirectory = _dir }));
        await tools.SendCode("System.Console.WriteLine(1);", CancellationToken.None);
        await server;

        Assert.NotNull(received);
        Assert.Equal("send_code", received!.Command);
        Assert.Equal("System.Console.WriteLine(1);", (string?)received.Params["code"]);
        Assert.Null(received.Params["source"]);
    }
}
