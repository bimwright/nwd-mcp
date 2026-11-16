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
        = env => NwdCommandResult.Success(env.Id, new JObject { ["echo"] = env.Command }, new NwdResponseMeta { DocumentId = DocumentId, DocumentTitle = "fake.nwd" });

    public const string DocumentId = "doc-1";

    public NwdMcpConfig Config { get; }
    public PluginClient Client { get; }
    public string TargetId { get; }

    /// <param name="documentTarget">Advertise document_target_v1 like a current plug-in.</param>
    /// <param name="confirmed">Confirm the document (nwd_switch_target verify=true) so writes are sent; the health check it sends is not kept in <see cref="Received"/>.</param>
    public FakePlugin(bool readOnly = false, bool documentTarget = true, bool confirmed = true)
    {
        Directory.CreateDirectory(_dir);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var pid = Environment.ProcessId;
        TargetId = "navis-2025-" + pid;
        File.WriteAllText(Path.Combine(_dir, TargetId + ".json"), $$"""
        { "target_id": "{{TargetId}}", "navisworks_year": 2025, "process_id": {{pid}},
          "host_product": "Manage", "port": {{port}}, "auth_token": "token",
          "document_title": "fake.nwd", "capabilities": {{(documentTarget ? "[\"document_target_v1\"]" : "[]")}},
          "last_heartbeat_utc": "{{DateTimeOffset.UtcNow.UtcDateTime:O}}" }
        """);
        Config = new NwdMcpConfig
        {
            DescriptorDirectory = _dir,
            BakeDirectory = Path.Combine(_dir, "baked"),
            ReportDirectory = Path.Combine(_dir, "reports"),
            ReadOnly = readOnly
        };
        Client = new PluginClient(Config);
        _ = Task.Run(AcceptLoop);
        if (documentTarget && confirmed)
        {
            Client.SwitchTarget("pid:" + pid);
            // Off the xUnit sync context: blocking on it from a constructor can starve its threads.
            Task.Run(() => Client.ConfirmDocumentAsync(CancellationToken.None)).GetAwaiter().GetResult();
            Received.Clear();
        }
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
