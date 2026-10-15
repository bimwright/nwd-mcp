using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Logging;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Views.Toast;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Transport;

public sealed class TcpTransportServer : IDisposable
{
    private readonly PluginOptions _options;
    private readonly string _descriptorDir;
    private readonly object _lifecycle = new object();
    private readonly Dictionary<TcpClient, Thread> _clients = new Dictionary<TcpClient, Thread>();
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    private readonly Limits _limits;
    private bool _started;
    private bool _disposed;
    private TcpListener? _listener;
    private Thread? _acceptThread;
    private Timer? _heartbeatTimer;
    private volatile bool _running;
    private int _activeClients;
    private long _lastCommandUtcTicks;
    private volatile string? _documentTitle;
    private volatile string? _documentPath;
    private string _authToken = "";
    private string _targetId = "";
    private CommandDispatcher? _dispatcher;
    private IReadOnlyDictionary<string, INwdCommand>? _handlers;

    public TcpTransportServer(PluginOptions options, string descriptorDir) : this(options, descriptorDir, new Limits()) { }

    internal TcpTransportServer(PluginOptions options, string descriptorDir, Limits limits)
    {
        _options = options;
        _descriptorDir = descriptorDir;
        _limits = limits;
    }

    internal sealed class Limits
    {
        internal int Clients = 32;
        internal int FrameBytes = NwdCommandEnvelope.MaxSerializedBytes;
        internal int ReadTimeoutMs = 120000;
        internal int WriteTimeoutMs = 30000;
        internal int StopTimeoutMs = 2000;
    }

    public int Port { get; private set; }
    public string AuthToken => _authToken;
    public string TargetId => _targetId;
    public int ActiveClientCount => Volatile.Read(ref _activeClients);

    public DateTime? LastCommandUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastCommandUtcTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>Command name, response JSON, success, error message. Skips health_check.</summary>
    public Action<string, string, bool, string?>? CommandCompleted { get; set; }

    public void Start(IReadOnlyDictionary<string, INwdCommand> handlers)
    {
        lock (_lifecycle)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpTransportServer));
            if (_started) throw new InvalidOperationException("A transport may only be started once.");
            _started = true;
            try
            {
                _authToken = AuthTokenGenerator.Generate();
                _targetId = $"navis-{_options.NavisworksYear}-{Process.GetCurrentProcess().Id}";
                _handlers = handlers;
                _dispatcher = new CommandDispatcher(handlers, 10 * 1024 * 1024);
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start(); // Reserve the endpoint before publishing readiness.
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                RefreshDocumentInfo();
                WriteDescriptor(requireOwnership: false);
                _running = true;
                var listener = _listener;
                _acceptThread = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "BimwrightNwd-TcpAccept" };
                _acceptThread.Start();
                _heartbeatTimer = new Timer(_ => Heartbeat(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
            catch
            {
                _disposed = true;
                _running = false;
                _stop.Cancel();
                _heartbeatTimer?.Dispose();
                try { _listener?.Stop(); } catch { }
                TargetDescriptorWriter.Delete(_descriptorDir, _options.NavisworksYear, Process.GetCurrentProcess().Id, _authToken);
                throw;
            }
        }
    }

    public void Dispose()
    {
        Thread[] workers;
        lock (_lifecycle)
        {
            if (_disposed) return;
            _disposed = true;
            _running = false;
            _stop.Cancel();
            _heartbeatTimer?.Dispose();
            try { _listener?.Stop(); } catch { }
            foreach (var client in _clients.Keys) try { client.Close(); } catch { }
            workers = new Thread[_clients.Count];
            _clients.Values.CopyTo(workers, 0);
            TargetDescriptorWriter.Delete(_descriptorDir, _options.NavisworksYear, Process.GetCurrentProcess().Id, _authToken);
        }
        // UI marshaling is cancellable, so unload never waits for its own UI queue.
        // Work already executing in the host is not forcibly interrupted or retried.
        var remaining = Stopwatch.StartNew();
        Join(_acceptThread);
        foreach (var worker in workers) Join(worker);
        void Join(Thread? thread)
        {
            var budget = _limits.StopTimeoutMs - (int)remaining.ElapsedMilliseconds;
            if (thread != null && thread != Thread.CurrentThread && budget > 0) thread.Join(budget);
        }
    }

    private void AcceptLoop(TcpListener listener)
    {
        while (_running)
        {
            TcpClient client;
            try { client = listener.AcceptTcpClient(); }
            catch { return; }
            lock (_lifecycle)
            {
                if (!_running || _clients.Count >= _limits.Clients) { client.Close(); continue; }
                client.SendTimeout = _limits.WriteTimeoutMs;
                var thread = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "BimwrightNwd-TcpClient" };
                _clients.Add(client, thread);
                Interlocked.Increment(ref _activeClients);
                thread.Start();
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
            {
                var reader = new FrameReader(client, stream, _limits);
                string? line;
                while (_running && (line = reader.ReadLine()) != null)
                {
                    string responseJson = "";
                    string? commandName = null;
                    string? paramsJson = null;
                    bool? recordCalls = null;
                    var clock = Stopwatch.StartNew();
                    try
                    {
                        var env = JsonConvert.DeserializeObject<NwdCommandEnvelope>(line);
                        if (env == null)
                        {
                            responseJson = ErrorJson("INVALID_ARGUMENT", "Empty or invalid envelope.");
                        }
                        else if (env.AuthToken != _authToken)
                        {
                            responseJson = ErrorJson("UNAUTHORIZED", "Invalid authorization token.");
                        }
                        else if (env.TimeoutMs <= 0)
                        {
                            responseJson = ErrorJson("INVALID_ARGUMENT", "timeout_ms must be positive.");
                        }
                        else
                        {
                            using var requestStop = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                            requestStop.CancelAfter(Math.Max(1000, env.TimeoutMs));
                            commandName = env.Command;
                            recordCalls = env.RecordCalls;
                            paramsJson = env.Params != null ? env.Params.ToString(Formatting.None) : null;
                            NwdCommandResult? result = null;
                            var deadline = NwdCommandContext.DeadlineFor(DateTime.UtcNow, env.TimeoutMs);
                            void Dispatch()
                            {
                                lock (_lifecycle)
                                    if (!_running) throw new OperationCanceledException(_stop.Token);
                                // Queued behind other work until the caller gave up: skip it rather than
                                // run a command nobody will read.
                                if (DateTime.UtcNow >= deadline)
                                {
                                    result = NwdCommandResult.Fail(env.Id, "TIMEOUT",
                                        "Navisworks was busy until the caller's timeout; the command was skipped.",
                                        new NwdResponseMeta { TargetId = _targetId, NavisworksYear = _options.NavisworksYear });
                                    return;
                                }
                                var context = new NwdCommandContext
                                {
                                    ReadOnly = false, // plug-in receives dispatch context
                                    EnableSendCode = _options.EnableSendCode,
                                    NavisworksYear = _options.NavisworksYear,
                                    TargetId = _targetId,
                                    Commands = _handlers,
                                    DeadlineUtc = deadline,
                                    CancellationToken = requestStop.Token
                                };
                                result = _dispatcher!.Dispatch(context, env);
                                if (!string.Equals(env.Command, "send_code", StringComparison.Ordinal))
                                    RefreshDocumentInfo();
                            }

                            // send_code compiles here, off the UI thread, and marshals only the
                            // script run onto it (SendCodeHandler).
                            if (string.Equals(commandName, "send_code", StringComparison.Ordinal))
                                Dispatch();
                            else
                                NavisworksUiThreadInvoker.Invoke(Dispatch, requestStop.Token);
                            TouchLastCommand();
                            responseJson = JsonConvert.SerializeObject(result);
                            NotifyCompleted(commandName, responseJson, result != null && result.Ok, result?.Error?.Message);
                            clock.Stop();
                            NwdCallLog.Record(commandName, paramsJson, result != null && result.Ok, clock.ElapsedMilliseconds, result?.Error?.Message, responseJson, recordCalls);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        responseJson = ErrorJson("TIMEOUT", "The request wait ended. A command that already started may still have run; do not blindly retry writes.");
                    }
                    catch (Exception ex)
                    {
                        clock.Stop();
                        responseJson = ErrorJson("API_ERROR", $"dispatch error: {ex.Message}");
                        if (commandName != null)
                        {
                            TouchLastCommand();
                            NotifyCompleted(commandName, responseJson, false, ex.Message);
                            NwdCallLog.Record(commandName, paramsJson, false, clock.ElapsedMilliseconds, ex.Message, responseJson, recordCalls);
                        }
                    }
                    if (!_running) break;
                    writer.WriteLine(responseJson);
                }
            }
        }
        catch
        {
            // client disconnect or IO error
        }
        finally
        {
            lock (_lifecycle) _clients.Remove(client);
            Interlocked.Decrement(ref _activeClients);
        }
    }

    private void TouchLastCommand()
    {
        Interlocked.Exchange(ref _lastCommandUtcTicks, DateTime.UtcNow.Ticks);
    }

    private void NotifyCompleted(string? command, string responseJson, bool success, string? errorMessage)
    {
        if (!ToastCommands.ShouldToast(command))
            return;
        try
        {
            CommandCompleted?.Invoke(command!, responseJson, success, errorMessage);
        }
        catch
        {
        }
    }

    private string ErrorJson(string code, string message)
    {
        var meta = new NwdResponseMeta { TargetId = _targetId, NavisworksYear = _options.NavisworksYear };
        var r = NwdCommandResult.Fail(Guid.Empty, code, message, meta);
        return JsonConvert.SerializeObject(r);
    }

    /// <summary>
    /// Reads the active document's title and path. Call on the Navisworks UI thread only
    /// (Start from OnLoaded, after each dispatched command, and from the Idle hook); the heartbeat
    /// timer reuses the cached values because the API is not valid on its thread.
    /// </summary>
    public void RefreshDocumentInfo(bool force = false)
    {
        string? title = null;
        string? path = null;
        try
        {
            var doc = NW.Application.ActiveDocument;
            if (doc != null && !doc.IsClear)
            {
                title = doc.Title;
                path = doc.FileName;
            }
        }
        catch {}

        if (!force && title == _documentTitle && path == _documentPath)
            return;
        _documentTitle = title;
        _documentPath = path;
        lock (_lifecycle)
            if (_running) WriteDescriptor();
    }

    private void Heartbeat()
    {
        lock (_lifecycle)
        {
            if (!_running) return;
            try { WriteDescriptor(); }
            catch { } // No persistent diagnostic fallback or repeated-error storm.
        }
    }

    private void WriteDescriptor(bool requireOwnership = true)
    {
        var title = _documentTitle;
        var path = _documentPath;
        var d = new TargetDescriptor
        {
            TargetId = _targetId,
            NavisworksYear = _options.NavisworksYear,
            ProcessId = Process.GetCurrentProcess().Id,
            HostProduct = "Manage",
            Port = Port,
            AuthToken = _authToken,
            DocumentTitle = title,
            DocumentPath = path,
            LastHeartbeatUtc = DateTimeOffset.UtcNow
        };
        TargetDescriptorWriter.Write(_descriptorDir, d, requireOwnership);
    }

    private sealed class FrameReader
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly Limits _limits;
        private readonly byte[] _buffer = new byte[4096];
        private int _position;
        private int _length;

        internal FrameReader(TcpClient client, NetworkStream stream, Limits limits)
        { _client = client; _stream = stream; _limits = limits; }

        internal string? ReadLine()
        {
            var clock = Stopwatch.StartNew();
            using (var frame = new MemoryStream())
            {
                while (true)
                {
                    if (_position == _length)
                    {
                        var remaining = _limits.ReadTimeoutMs - (int)clock.ElapsedMilliseconds;
                        if (remaining <= 0) throw new IOException("Request frame timed out.");
                        _client.ReceiveTimeout = remaining;
                        _length = _stream.Read(_buffer, 0, _buffer.Length);
                        _position = 0;
                        if (_length == 0) return null; // A partial line is not a command.
                    }
                    var value = _buffer[_position++];
                    if (value == '\n') return new UTF8Encoding(false, true).GetString(frame.ToArray()).TrimEnd('\r');
                    if (frame.Length >= _limits.FrameBytes) throw new InvalidDataException("Request frame is too large.");
                    frame.WriteByte(value);
                }
            }
        }
    }
}

internal static class AuthTokenGenerator
{
    public static string Generate()
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
