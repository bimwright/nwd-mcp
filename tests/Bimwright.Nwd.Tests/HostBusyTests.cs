using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Transport;
using Bimwright.Nwd.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>HOST_BUSY / TIMEOUT split and health while busy (family contract FC-BUSY-01..05, plan M4).</summary>
[Collection("Call log")]
public sealed class HostBusyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nwd-busy-" + Guid.NewGuid().ToString("N"));
    public HostBusyTests() => Directory.CreateDirectory(_dir);

    // ---- StartGate: the start/abandon decision ----

    [Fact]
    public void Work_started_in_time_runs_once()
    {
        var runs = 0;
        Assert.True(StartGate.Run(run => Task.Run(run), () => Interlocked.Increment(ref runs), DateTime.UtcNow.AddSeconds(5), CancellationToken.None));
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Work_not_started_by_the_deadline_never_runs_later()
    {
        Action? queued = null;
        var runs = 0;
        Assert.False(StartGate.Run(run => queued = run, () => runs++, DateTime.UtcNow.AddMilliseconds(50), CancellationToken.None));
        queued!();   // the UI thread frees up afterwards
        Assert.Equal(0, runs);
    }

    [Fact]
    public void Cancellation_before_start_reports_not_started_and_the_work_never_runs()
    {
        Action? queued = null;
        var runs = 0;
        using var cancel = new CancellationTokenSource(50);
        Assert.False(StartGate.Run(run => queued = run, () => runs++, DateTime.UtcNow.AddSeconds(30), cancel.Token));
        queued!();
        Assert.Equal(0, runs);
    }

    [Fact]
    public void Cancellation_after_start_means_outcome_unknown()
    {
        using var release = new ManualResetEventSlim();
        using var cancel = new CancellationTokenSource(100);
        Assert.Throws<OperationCanceledException>(() =>
            StartGate.Run(run => Task.Run(run), () => release.Wait(5000), DateTime.UtcNow.AddSeconds(30), cancel.Token));
        release.Set();
    }

    [Fact]
    public void Exceptions_from_the_work_keep_their_type()
        => Assert.Throws<InvalidOperationException>(() =>
            StartGate.Run(run => Task.Run(run), () => throw new InvalidOperationException("x"), DateTime.UtcNow.AddSeconds(5), CancellationToken.None));

    [Fact]
    public void Reported_outcome_always_matches_whether_the_work_ran()
    {
        var random = new Random(7);
        for (var i = 0; i < 300; i++)
        {
            var ran = 0;
            var delay = random.Next(0, 4);
            var done = new ManualResetEventSlim();
            var started = StartGate.Run(run => Task.Run(() => { Thread.Sleep(delay); run(); done.Set(); }),
                () => Interlocked.Increment(ref ran), DateTime.UtcNow.AddMilliseconds(2), CancellationToken.None);
            Assert.True(done.Wait(2000));
            Assert.Equal(started ? 1 : 0, ran);
        }
    }

    // ---- policy ----

    [Theory]
    [InlineData("save_viewpoint", true)]
    [InlineData("send_code", true)]
    [InlineData("health_check", false)]          // answers from the transport thread
    [InlineData("report_task_result", false)]    // touches no Navisworks API
    public void Modal_dialog_refuses_commands_but_not_health_or_reports(string command, bool refused)
    {
        Assert.Equal(refused, HostBusyPolicy.RefusesForModal(command, modalOpen: true));
        Assert.False(HostBusyPolicy.RefusesForModal(command, modalOpen: false));
    }

    [Fact]
    public void Busy_messages_say_not_started_and_timeout_says_unknown()
    {
        Assert.Contains("was not started and will not run later", HostBusyPolicy.ModalMessage("hide_items", "Options Editor"));
        Assert.Contains("\"Options Editor\"", HostBusyPolicy.ModalMessage("hide_items", "Options Editor"));
        Assert.Contains("was not started and will not run later", HostBusyPolicy.ExpiredMessage("hide_items", 30000));
        var timeout = HostBusyPolicy.StartedTimeoutMessage("hide_items", 30000);
        Assert.Contains("outcome is unknown", timeout);
        Assert.DoesNotContain("not started", timeout);
    }

    [Theory]
    [InlineData(false, false, false, null)]
    [InlineData(true, false, true, "modal_dialog")]
    [InlineData(false, true, true, "ui_thread_busy")]
    public void Health_busy_fields(bool modal, bool ui, bool busy, string? reason)
    {
        var data = new JObject().WithHostBusy(modal, ui);
        Assert.Equal(busy, (bool)data["host_busy"]!);
        Assert.Equal(reason, (string?)data["busy_reason"]);
    }

    // ---- plug-in transport (real TcpTransportServer, UI thread simulated) ----

    [Fact]
    public async Task Command_queued_behind_a_busy_UI_thread_returns_HOST_BUSY_and_never_runs()
    {
        using var release = new ManualResetEventSlim();
        NavisworksUiThreadInvoker.Override = action => { release.Wait(10000); action(); };
        var runs = 0;
        using var server = Server(("test", () => { Interlocked.Increment(ref runs); return new JObject(); }));

        var clock = Stopwatch.StartNew();
        var response = await Exchange(server, "test", timeoutMs: 3000);   // start deadline: timeout - 2 s

        Assert.Equal("HOST_BUSY", (string?)response["error"]?["code"]);
        Assert.Contains("was not started and will not run later", (string?)response["error"]?["message"]);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2.5), $"answered after {clock.Elapsed}");
        release.Set();
        await Task.Delay(200);
        Assert.Equal(0, Volatile.Read(ref runs));
    }

    [Fact]
    public async Task Command_that_started_and_overran_returns_TIMEOUT_with_unknown_outcome()
    {
        NavisworksUiThreadInvoker.Override = action => action();
        using var release = new ManualResetEventSlim();
        using var server = Server(("test", () => { release.Wait(10000); return new JObject(); }));

        var response = await Exchange(server, "test", timeoutMs: 3000);   // answered 1 s before the gateway gives up

        Assert.Equal("TIMEOUT", (string?)response["error"]?["code"]);
        Assert.Contains("outcome is unknown", (string?)response["error"]?["message"]);
        release.Set();
    }

    [Fact]
    public async Task Command_cancelled_while_running_is_TIMEOUT_not_API_ERROR()
    {
        NavisworksUiThreadInvoker.Override = action => action();
        using var server = new TcpTransportServer(new PluginOptions(2025, false, 0), _dir, new TcpTransportServer.Limits());
        server.Start(new Dictionary<string, INwdCommand> { ["test"] = new Observing() });

        var response = await Exchange(server, "test", timeoutMs: 3000);

        Assert.Equal("TIMEOUT", (string?)response["error"]?["code"]);
        Assert.Contains("outcome is unknown", (string?)response["error"]?["message"]);
    }

    [Fact]
    public async Task Health_answers_without_the_UI_thread_while_it_is_busy()
    {
        using var release = new ManualResetEventSlim();
        NavisworksUiThreadInvoker.Override = action => { release.Wait(10000); action(); };
        var runs = 0;
        using var server = Server(("health_check", () => { Interlocked.Increment(ref runs); return new JObject { ["from_ui"] = true }; }));

        var clock = Stopwatch.StartNew();
        var response = await Exchange(server, "health_check", timeoutMs: 30000);

        Assert.True((bool)response["ok"]!);
        Assert.True((bool)response["data"]!["host_busy"]!);
        Assert.Equal("ui_thread_busy", (string?)response["data"]!["busy_reason"]);
        Assert.True((bool)response["data"]!["answered_without_ui_thread"]!);
        Assert.Equal("plugin", (string?)response["data"]!["setup_plugin"]?["component"]);
        Assert.Null(response["meta"]?["document_id"]);   // verify cannot confirm a document it could not read
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"answered after {clock.Elapsed}");
        release.Set();
        await Task.Delay(200);
        Assert.Equal(0, Volatile.Read(ref runs));
    }

    [Fact]
    public async Task Health_runs_on_the_UI_thread_when_it_is_free()
    {
        NavisworksUiThreadInvoker.Override = action => action();
        using var server = Server(("health_check", () => new JObject { ["from_ui"] = true }));
        var response = await Exchange(server, "health_check", timeoutMs: 30000);
        Assert.True((bool)response["data"]!["from_ui"]!);
    }

    // ---- gateway ----

    [Fact]
    public async Task Verify_while_Navisworks_is_busy_is_HOST_BUSY_not_a_pairing_error()
    {
        using var plugin = new FakePlugin(confirmed: false);
        plugin.Reply = env => NwdCommandResult.Success(env.Id,
            new JObject { ["healthy"] = true, ["answered_without_ui_thread"] = true }.WithHostBusy(modalOpen: true, uiThreadBusy: false),
            new NwdResponseMeta());
        var verify = JObject.Parse(await new MetaTools(plugin.Client).SwitchTarget("pid:" + Environment.ProcessId, verify: true));
        Assert.Equal("HOST_BUSY", (string?)verify["error"]?["code"]);
        Assert.Contains("modal_dialog", (string?)verify["error"]?["message"]);
    }

    [Fact]
    public async Task Plugin_HOST_BUSY_reaches_the_agent_with_its_code()
    {
        using var plugin = new FakePlugin();
        plugin.Reply = env => NwdCommandResult.Fail(env.Id, "HOST_BUSY", HostBusyPolicy.ModalMessage(env.Command, "Options Editor"), new NwdResponseMeta());
        var ex = await Assert.ThrowsAsync<Bimwright.Nwd.Server.NwdGatewayException>(() => plugin.Client.SendAsync("save_viewpoint", new JObject(), CancellationToken.None));
        Assert.Equal("HOST_BUSY", ex.Code);
        Assert.Contains("will not run later", ex.Message);
    }

    // ---- helpers ----

    private TcpTransportServer Server(params (string Name, Func<JObject> Execute)[] commands)
    {
        var server = new TcpTransportServer(new PluginOptions(2025, false, 0), _dir, new TcpTransportServer.Limits());
        server.Start(commands.ToDictionary(c => c.Name, c => (INwdCommand)new Command(c.Name, c.Execute)));
        return server;
    }

    private static async Task<JObject> Exchange(TcpTransportServer server, string command, int timeoutMs)
    {
        using var peer = new TcpClient();
        await peer.ConnectAsync("127.0.0.1", server.Port);
        var stream = peer.GetStream();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new NwdCommandEnvelope
        { Id = Guid.NewGuid(), AuthToken = server.AuthToken, Command = command, Params = new JObject(), TimeoutMs = timeoutMs, RecordCalls = false }) + "\n"));
        using var reader = new StreamReader(stream);
        return JObject.Parse((await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)))!);
    }

    private sealed class Command(string name, Func<JObject> execute) : INwdCommand
    {
        public string Name => name;
        public bool IsReadOnly => true;
        public NwdCommandResult Execute(NwdCommandContext context, JObject parameters)
            => NwdCommandResult.Success(Guid.Empty, execute(), new NwdResponseMeta());
    }

    /// <summary>A handler that stops when the caller's wait ends, like a script observing its token.</summary>
    private sealed class Observing : INwdCommand
    {
        public string Name => "test";
        public bool IsReadOnly => true;
        public NwdCommandResult Execute(NwdCommandContext context, JObject parameters)
        {
            context.CancellationToken.WaitHandle.WaitOne(10000);
            context.CancellationToken.ThrowIfCancellationRequested();
            return NwdCommandResult.Success(Guid.Empty, new JObject(), new NwdResponseMeta());
        }
    }

    public void Dispose()
    {
        NavisworksUiThreadInvoker.Override = null;
        try { Directory.Delete(_dir, true); } catch { }
    }
}
