using System.Net.Sockets;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Logging;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Transport;

namespace Bimwright.Nwd.Tests;

[Collection("Call log")]
public sealed class Issue22RegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nwd-i22-" + Guid.NewGuid().ToString("N"));

    public Issue22RegressionTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("mcp-calls.jsonl")]
    [InlineData("nwd-mcp-calls.jsonl")]
    public void Actual_call_logger_never_appends_past_file_budget(string name)
    {
        NwdCallLog.DirectoryOverride = _directory;
        var path = Path.Combine(_directory, name);
        using (var seed = File.Create(path)) seed.SetLength(5 * 1024 * 1024 - 64);
        NwdCallLog.Record("get_document_info", "{}", true, 1, null, "{}", true, name);
        Assert.True(new FileInfo(path).Length <= 5 * 1024 * 1024, "The next record crossed the 5 MiB cap.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_closes_an_already_accepted_idle_or_partial_client(bool partial)
    {
        using var server = new TcpTransportServer(new PluginOptions(2025, false, 0), _directory);
        server.Start(new Dictionary<string, INwdCommand>());
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.Port);
        Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 1, 2000));
        if (partial) await client.GetStream().WriteAsync(new byte[] { (byte)'{' });
        server.Dispose();
        Assert.True(SpinWait.SpinUntil(() => server.ActiveClientCount == 0, 1000),
            "Dispose left the accepted client worker blocked in ReadLine.");
        // Closing with unread request bytes makes Windows send RST instead of FIN; both mean closed.
        try { Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(1))); }
        catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset }) { }
    }

    public void Dispose()
    {
        NwdCallLog.DirectoryOverride = null;
        NavisworksUiThreadInvoker.Override = null;
        try { Directory.Delete(_directory, true); } catch { }
    }
}
