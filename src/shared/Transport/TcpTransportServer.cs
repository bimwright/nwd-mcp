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
    private TcpListener? _listener;
    private Thread? _acceptThread;
    private Timer? _heartbeatTimer;
    private volatile bool _running;
    private int _activeClients;
    private long _lastCommandUtcTicks;
    private string _authToken = "";
    private string _targetId = "";
    private CommandDispatcher? _dispatcher;
    private IReadOnlyDictionary<string, INwdCommand>? _handlers;

    public TcpTransportServer(PluginOptions options, string descriptorDir)
    {
        _options = options;
        _descriptorDir = descriptorDir;
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
        _authToken = AuthTokenGenerator.Generate();
        var pid = Process.GetCurrentProcess().Id;
        _targetId = $"navis-{_options.NavisworksYear}-{pid}";

        _handlers = handlers;
        _dispatcher = new CommandDispatcher(handlers, 10 * 1024 * 1024);

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        WriteDescriptor();
        _running = true;

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "BimwrightNwd-TcpAccept" };
        _acceptThread.Start();

        _heartbeatTimer = new Timer(_ => WriteDescriptor(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        _running = false;
        _heartbeatTimer?.Dispose();
        try { _listener?.Stop(); } catch {}
        TargetDescriptorWriter.Delete(_descriptorDir, _options.NavisworksYear, Process.GetCurrentProcess().Id);
    }

    private void AcceptLoop()
    {
        while (_running && _listener != null)
        {
            TcpClient client;
            try { client = _listener.AcceptTcpClient(); }
            catch { return; }
            var t = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "BimwrightNwd-TcpClient" };
            t.Start();
        }
    }

    private void HandleClient(TcpClient client)
    {
        Interlocked.Increment(ref _activeClients);
        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string responseJson = "";
                    string? commandName = null;
                    string? paramsJson = null;
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
                        else
                        {
                            commandName = env.Command;
                            paramsJson = env.Params != null ? env.Params.ToString(Formatting.None) : null;
                            NwdCommandResult? result = null;
                            void Dispatch()
                            {
                                var context = new NwdCommandContext
                                {
                                    ReadOnly = false, // plug-in receives dispatch context
                                    EnableSendCode = _options.EnableSendCode,
                                    NavisworksYear = _options.NavisworksYear,
                                    TargetId = _targetId,
                                    Commands = _handlers
                                };
                                result = _dispatcher!.Dispatch(context, env);
                            }

                            // send_code waits up to 30s for its script thread. Doing that wait
                            // inside SynchronizationContext.Send freezes Navisworks and deadlocks
                            // a script that calls back onto the UI thread.
                            if (string.Equals(commandName, "send_code", StringComparison.Ordinal))
                                Dispatch();
                            else
                                NavisworksUiThreadInvoker.Invoke(Dispatch);
                            TouchLastCommand();
                            responseJson = JsonConvert.SerializeObject(result);
                            NotifyCompleted(commandName, responseJson, result != null && result.Ok, result?.Error?.Message);
                            clock.Stop();
                            NwdCallLog.Record(commandName, paramsJson, result != null && result.Ok, clock.ElapsedMilliseconds, result?.Error?.Message, responseJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        clock.Stop();
                        responseJson = ErrorJson("API_ERROR", $"dispatch error: {ex.Message}");
                        if (commandName != null)
                        {
                            TouchLastCommand();
                            NotifyCompleted(commandName, responseJson, false, ex.Message);
                            NwdCallLog.Record(commandName, paramsJson, false, clock.ElapsedMilliseconds, ex.Message, responseJson);
                        }
                    }
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

    private void WriteDescriptor()
    {
        string? title = null;
        string? path = null;
        try
        {
            // Navisworks active document checks
            var doc = NW.Application.ActiveDocument;
            if (doc != null)
            {
                title = doc.Title;
                path = doc.FileName;
            }
        }
        catch {}

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
        TargetDescriptorWriter.Write(_descriptorDir, d);
    }
}

internal static class AuthTokenGenerator
{
    public static string Generate()
    {
        using var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
