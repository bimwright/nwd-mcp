using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Transport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>
/// FC-TST-03: the plug-in half of the wire. The real transport hands each authorized command's client_name to the
/// toast, health checks included, and clears it when a command carries none.
/// </summary>
[Collection("Call log")]
public sealed class ClientNameTransportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-clientname-transport-" + Guid.NewGuid().ToString("N"));

    public ClientNameTransportTests() => Directory.CreateDirectory(_dir);

    // FC-TST-03
    [Fact]
    public async Task Authorized_commands_report_their_client_name_and_a_missing_name_clears_it()
    {
        NavisworksUiThreadInvoker.Override = action => action();
        var seen = new ConcurrentQueue<string?>();
        using var server = Server();
        server.ClientNameSeen = name => seen.Enqueue(name);

        await Exchange(server, "test", server.AuthToken, "claude-code");
        await Exchange(server, "health_check", server.AuthToken, "codex-mcp-client");
        await Exchange(server, "test", server.AuthToken, null);

        Assert.Equal(new string?[] { "claude-code", "codex-mcp-client", null }, seen.ToArray());
    }

    // FC-TST-03
    [Fact]
    public async Task An_unauthorized_command_does_not_name_the_agent()
    {
        NavisworksUiThreadInvoker.Override = action => action();
        var seen = new ConcurrentQueue<string?>();
        using var server = Server();
        server.ClientNameSeen = name => seen.Enqueue(name);

        var response = await Exchange(server, "test", "wrong-token", "claude-code");

        Assert.Contains("UNAUTHORIZED", response.ToString(Formatting.None));
        Assert.Empty(seen);
    }

    private TcpTransportServer Server()
    {
        var server = new TcpTransportServer(new PluginOptions(2025, false, 0), _dir, new TcpTransportServer.Limits());
        server.Start(new Dictionary<string, INwdCommand>
        {
            ["test"] = new Command("test"),
            ["health_check"] = new Command("health_check"),
        });
        return server;
    }

    private static async Task<JObject> Exchange(TcpTransportServer server, string command, string token, string? clientName)
    {
        using var peer = new TcpClient();
        await peer.ConnectAsync("127.0.0.1", server.Port);
        var stream = peer.GetStream();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new NwdCommandEnvelope
        {
            Id = Guid.NewGuid(), AuthToken = token, Command = command, Params = new JObject(), TimeoutMs = 30000,
            RecordCalls = false, ClientName = clientName
        }) + "\n"));
        using var reader = new StreamReader(stream);
        return JObject.Parse((await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)))!);
    }

    private sealed class Command(string name) : INwdCommand
    {
        public string Name => name;
        public bool IsReadOnly => true;
        public NwdCommandResult Execute(NwdCommandContext context, JObject parameters)
            => NwdCommandResult.Success(Guid.Empty, new JObject(), new NwdResponseMeta());
    }

    public void Dispose()
    {
        NavisworksUiThreadInvoker.Override = null;
        try { Directory.Delete(_dir, true); } catch { }
    }
}
