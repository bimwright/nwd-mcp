using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Transport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

[Collection("Call log")]
public sealed class TcpTransportLifecycleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-tcp-" + Guid.NewGuid().ToString("N"));
    public TcpTransportLifecycleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void Start_is_one_shot_and_publication_failure_releases_listener()
    {
        using var server = Server();
        server.Start(Handlers());
        Assert.Throws<InvalidOperationException>(() => server.Start(Handlers()));
        server.Dispose();
        Assert.Throws<ObjectDisposedException>(() => server.Start(Handlers()));
        Assert.Empty(Directory.GetFiles(_dir));

        var blocked = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocked, "fixture");
        using var failed = new TcpTransportServer(new PluginOptions(2025, false, 0), blocked);
        Assert.Throws<IOException>(() => failed.Start(Handlers()));
        using var peer = new TcpClient();
        Assert.ThrowsAny<SocketException>(() => peer.Connect("127.0.0.1", failed.Port));
        Assert.Throws<ObjectDisposedException>(() => failed.Start(Handlers()));
    }

    [Fact]
    public void Old_dispose_and_refresh_cannot_remove_or_overwrite_replacement_descriptor()
    {
        using var old = Server();
        old.Start(Handlers());
        using var replacement = Server();
        replacement.Start(Handlers());
        old.RefreshDocumentInfo(force: true);
        old.Dispose();
        var path = TargetDescriptorWriter.GetPath(_dir, 2025, Process.GetCurrentProcess().Id);
        Assert.Equal(replacement.AuthToken, JObject.Parse(File.ReadAllText(path)).Value<string>("auth_token"));
        replacement.Dispose();
        replacement.RefreshDocumentInfo(force: true);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Atomic_heartbeat_publication_remains_readable_during_replacement()
    {
        using var server = Server();
        server.Start(Handlers());
        var registry = new TargetRegistry(_dir);
        var writer = Task.Run(() => { for (var i = 0; i < 100; i++) server.RefreshDocumentInfo(force: true); });
        for (var i = 0; i < 100; i++) Assert.Single(registry.List());
        await writer;
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
    }

    [Fact]
    public async Task Existing_gateway_refreshes_endpoint_and_token_after_same_pid_replacement()
    {
        using var old = Server();
        old.Start(Handlers(() => new JObject { ["generation"] = 1 }));
        var gateway = new PluginClient(new NwdMcpConfig { DescriptorDirectory = _dir, TimeoutMs = 2000 });
        Assert.Equal(1, (int)(await gateway.SendAsync("test", new { }, CancellationToken.None))["generation"]!);
        old.Dispose();
        using var replacement = Server();
        replacement.Start(Handlers(() => new JObject { ["generation"] = 2 }));
        Assert.Equal(2, (int)(await gateway.SendAsync("test", new { }, CancellationToken.None))["generation"]!);
    }

    [Fact]
    public async Task Stop_cancels_queued_ui_wait_and_skips_command_that_has_not_started()
    {
        using var queued = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        NavisworksUiThreadInvoker.Override = action => { queued.Set(); release.Wait(5000); action(); };
        using var server = Server();
        server.Start(Handlers(() => { Interlocked.Increment(ref calls); return new JObject(); }));
        using var peer = await Connect(server);
        await Send(peer, server);
        Assert.True(queued.Wait(2000));
        var clock = Stopwatch.StartNew();
        server.Dispose();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Equal(0, server.ActiveClientCount);
        release.Set();
        Assert.Equal(0, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Stop_does_not_abort_or_retry_work_already_running()
    {
        using var running = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        using var server = Server(new TcpTransportServer.Limits { StopTimeoutMs = 100 });
        server.Start(Handlers(() => { Interlocked.Increment(ref calls); running.Set(); release.Wait(5000); return new JObject(); }));
        using var peer = await Connect(server);
        await Send(peer, server);
        Assert.True(running.Wait(2000));
        server.Dispose();
        Assert.Equal(1, Volatile.Read(ref calls));
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 0, 2000));
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Admission_is_bounded_and_stop_closes_all_idle_clients()
    {
        using var server = Server(new TcpTransportServer.Limits { Clients = 2 });
        server.Start(Handlers());
        using var first = await Connect(server);
        using var second = await Connect(server);
        Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 2, 2000));
        using var excess = await Connect(server);
        Assert.Equal(0, await excess.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, server.ActiveClientCount);
        server.Dispose();
        Assert.Equal(0, server.ActiveClientCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unterminated_or_oversize_utf8_frame_is_closed_without_dispatch(bool oversize)
    {
        var calls = 0;
        using var server = Server(new TcpTransportServer.Limits { FrameBytes = 64, ReadTimeoutMs = 100 });
        server.Start(Handlers(() => { calls++; return new JObject(); }));
        using var peer = await Connect(server);
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes(oversize ? new string('é', 40) + "\n" : "{"));
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 0, 2000));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Slow_reader_write_times_out_without_a_response_queue()
    {
        using var server = Server(new TcpTransportServer.Limits { WriteTimeoutMs = 100 });
        server.Start(Handlers(() => new JObject { ["payload"] = new string('x', 9 * 1024 * 1024) }));
        using var peer = new TcpClient { ReceiveBufferSize = 1024 };
        await peer.ConnectAsync("127.0.0.1", server.Port);
        await Send(peer, server);
        Assert.True(SpinWait.SpinUntil(() => server.LastCommandUtc != null, 3000));
        Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 0, 3000), "Blocked write retained its client worker.");
    }

    [Fact]
    public async Task Idle_read_deadline_does_not_cut_short_a_legitimate_running_command()
    {
        using var server = Server(new TcpTransportServer.Limits { ReadTimeoutMs = 50 });
        server.Start(Handlers(() => { Thread.Sleep(150); return new JObject { ["completed"] = true }; }));
        using var peer = await Connect(server);
        await Send(peer, server, timeoutMs: 300000);
        using var reader = new StreamReader(peer.GetStream());
        var response = JObject.Parse((await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)))!);
        Assert.True(response.Value<bool>("ok"));
        Assert.True(response["data"]!.Value<bool>("completed"));
    }

    private TcpTransportServer Server(TcpTransportServer.Limits? limits = null)
        => new(new PluginOptions(2025, false, 0), _dir, limits ?? new TcpTransportServer.Limits());

    private static IReadOnlyDictionary<string, INwdCommand> Handlers(Func<JObject>? execute = null)
        => new Dictionary<string, INwdCommand> { ["test"] = new TestCommand(execute ?? (() => new JObject())) };

    private static async Task<TcpClient> Connect(TcpTransportServer server)
    { var client = new TcpClient(); await client.ConnectAsync("127.0.0.1", server.Port); return client; }

    private static Task Send(TcpClient peer, TcpTransportServer server, int timeoutMs = 30000)
        => peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new NwdCommandEnvelope
        { Id = Guid.NewGuid(), AuthToken = server.AuthToken, Command = "test", Params = new JObject(), TimeoutMs = timeoutMs, RecordCalls = false }) + "\n")).AsTask();

    private sealed class TestCommand(Func<JObject> execute) : INwdCommand
    {
        public string Name => "test";
        public bool IsReadOnly => true;
        public NwdCommandResult Execute(NwdCommandContext context, JObject parameters)
            => NwdCommandResult.Success(Guid.Empty, execute(), new NwdResponseMeta());
    }

    public void Dispose()
    {
        NavisworksUiThreadInvoker.Override = null;
        Directory.Delete(_dir, true);
    }
}
