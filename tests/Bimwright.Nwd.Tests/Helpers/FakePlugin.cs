using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests.Helpers;

/// <summary>
/// Loopback stand-in for the Navisworks plug-in: writes a live descriptor, accepts the
/// server's NDJSON envelopes, records them, and answers with <see cref="Reply"/>.
/// </summary>
internal sealed class FakePlugin : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-fake-" + Guid.NewGuid().ToString("N"));
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();

    public ConcurrentQueue<NwdCommandEnvelope> Received { get; } = new();

    public Func<NwdCommandEnvelope, NwdCommandResult> Reply { get; set; }
        = env => NwdCommandResult.Success(env.Id, new JObject { ["echo"] = env.Command }, new NwdResponseMeta());

    public NwdMcpConfig Config { get; }
    public PluginClient Client { get; }
    public string TargetId { get; }

    public FakePlugin(bool readOnly = false)
    {
        Directory.CreateDirectory(_dir);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var pid = Environment.ProcessId;
        TargetId = "navis-2025-" + pid;
        File.WriteAllText(Path.Combine(_dir, TargetId + ".json"), $$"""
        { "target_id": "{{TargetId}}", "navisworks_year": 2025, "process_id": {{pid}},
          "host_product": "Manage", "port": {{port}}, "auth_token": "token",
          "document_title": "fake.nwd",
          "last_heartbeat_utc": "{{DateTimeOffset.UtcNow.UtcDateTime:O}}" }
        """);
        Config = new NwdMcpConfig
        {
            DescriptorDirectory = _dir,
            BakeDirectory = Path.Combine(_dir, "baked"),
            ReadOnly = readOnly
        };
        Client = new PluginClient(Config);
        _ = Task.Run(AcceptLoop);
    }

    public NwdCommandEnvelope Single()
    {
        var all = Received.ToArray();
        Assert.True(all.Length == 1, $"expected one plug-in call, got {all.Length}: {string.Join(", ", all.Select(e => e.Command))}");
        return all[0];
    }

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch { return; }
            _ = Task.Run(async () =>
            {
                using (client)
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var env = JsonConvert.DeserializeObject<NwdCommandEnvelope>(line)!;
                        Received.Enqueue(env);
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(Reply(env)));
                    }
                }
            });
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        try { Directory.Delete(_dir, true); } catch { }
    }
}
