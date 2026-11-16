using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class PluginClientCancellationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-client-cancel-" + Guid.NewGuid().ToString("N"));
    public PluginClientCancellationTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task Cancelled_request_preserves_cancellation_and_closes_the_socket()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = Client(listener, 30000);
        using var stop = new CancellationTokenSource();
        var request = client.SendAsync("test", new { }, stop.Token);
        using var peer = await listener.AcceptTcpClientAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        using var reader = new StreamReader(peer.GetStream());
        Assert.NotNull(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Null(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Timeout_is_finite_and_keeps_unknown_execution_warning()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = Client(listener, 100);
        var request = client.SendAsync("test", new { }, CancellationToken.None);
        using var peer = await listener.AcceptTcpClientAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        using var reader = new StreamReader(peer.GetStream());
        Assert.NotNull(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        var error = await Assert.ThrowsAsync<NwdGatewayException>(() => request);
        Assert.Equal("TIMEOUT", error.Code);
        Assert.Contains("may already have run", error.Message);
        Assert.Null(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_timeout_cannot_create_an_infinite_wait(int timeout)
    {
        var client = new PluginClient(new NwdMcpConfig { DescriptorDirectory = _dir, TimeoutMs = timeout });
        var error = await Assert.ThrowsAsync<NwdGatewayException>(() => client.SendAsync("test", new { }, CancellationToken.None));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }

    [Fact]
    public async Task Oversize_unterminated_response_is_rejected_before_waiting_for_a_newline()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var request = Client(listener, 2000, 256).SendAsync("test", new { }, CancellationToken.None);
        using var peer = await listener.AcceptTcpClientAsync();
        using var reader = new StreamReader(peer.GetStream());
        await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes(new string('é', 200)));
        var error = await Assert.ThrowsAsync<NwdGatewayException>(() => request.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal("RESPONSE_TOO_LARGE", error.Code);
        Assert.Contains("may already have run", error.Message);
    }

    [Fact]
    public async Task Exact_utf8_response_budget_accepts_windows_line_ending()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var reply = JsonConvert.SerializeObject(NwdCommandResult.Success(Guid.NewGuid(), new JObject { ["text"] = "Tiếng Việt" }, new NwdResponseMeta()));
        var request = Client(listener, 2000, Encoding.UTF8.GetByteCount(reply)).SendAsync("test", new { }, CancellationToken.None);
        using var peer = await listener.AcceptTcpClientAsync();
        using var reader = new StreamReader(peer.GetStream());
        await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes(reply + "\r\n"));
        Assert.Equal("Tiếng Việt", (string?)(await request)["text"]);
    }

    [Fact]
    public async Task Oversize_request_is_rejected_before_connecting_or_dispatching()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = Client(listener, 2000);
        // A wire command without a write gate, so the size limit is what refuses it.
        var error = await Assert.ThrowsAsync<NwdGatewayException>(() => client.SendAsync("test",
            new { code = new string('x', NwdCommandEnvelope.MaxSerializedBytes) }, CancellationToken.None));
        Assert.Equal("REQUEST_TOO_LARGE", error.Code);
        Assert.False(listener.Pending());
    }

    private PluginClient Client(TcpListener listener, int timeout, int responseBudget = 10 * 1024 * 1024)
    {
        File.WriteAllText(Path.Combine(_dir, "target.json"), JsonConvert.SerializeObject(new TargetDescriptor
        {
            TargetId = "cancel-fixture", AuthToken = "fixture", HostProduct = "Manage", NavisworksYear = 2025,
            ProcessId = Process.GetCurrentProcess().Id, LastHeartbeatUtc = DateTimeOffset.UtcNow,
            Port = ((IPEndPoint)listener.LocalEndpoint).Port
        }));
        return new PluginClient(new NwdMcpConfig { DescriptorDirectory = _dir, TimeoutMs = timeout, MaxResponseBytes = responseBudget });
    }

    public void Dispose() => Directory.Delete(_dir, true);
}
