using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

public sealed class NwdGatewayException : Exception
{
    public string Code { get; }
    public NwdGatewayException(string code, string message) : base(message) => Code = code;
}

public sealed class PluginClient
{
    private readonly NwdMcpConfig _config;
    private readonly TargetRegistry _registry;
    private TargetDescriptor? _current;

    public PluginClient(NwdMcpConfig config)
    {
        _config = config;
        _registry = new TargetRegistry(config.DescriptorDirectory);
    }

    public IReadOnlyList<TargetDescriptor> ListTargets() => _registry.List();

    public TargetDescriptor? CurrentTarget
    {
        get
        {
            var live = _registry.List();
            if (_current is not null && live.Any(t => t.TargetId == _current.TargetId)) return _current;
            _current = (_config.TargetId is { } id ? live.FirstOrDefault(t => t.TargetId == id) : null) ?? live.FirstOrDefault();
            return _current;
        }
    }

    public bool SwitchTarget(string targetId)
    {
        var match = _registry.List().FirstOrDefault(t => t.TargetId == targetId);
        if (match is null) return false;
        _current = match;
        return true;
    }

    public async Task<JToken> SendAsync(string command, object parameters, CancellationToken ct)
    {
        var target = CurrentTarget ?? throw new NwdGatewayException("NO_TARGET", "No live Navisworks Manage target. Start Navisworks with the nwd plug-in loaded.");
        var env = new NwdCommandEnvelope
        {
            Id = Guid.NewGuid(),
            Command = command,
            Params = parameters as JObject ?? JObject.FromObject(parameters),
            TimeoutMs = _config.TimeoutMs,
            AuthToken = target.AuthToken
        };

        using var client = new TcpClient();
        try
        {
            var connect = client.ConnectAsync("127.0.0.1", target.Port);
            if (await Task.WhenAny(connect, Task.Delay(_config.TimeoutMs, ct)) != connect)
                throw new NwdGatewayException("TIMEOUT", $"connect to target {target.TargetId} timed out");
            await connect;
        }
        catch (NwdGatewayException) { throw; }
        catch (Exception ex)
        {
            throw new NwdGatewayException("TARGET_UNAVAILABLE", $"cannot reach target {target.TargetId}: {ex.Message}");
        }

        using var stream = client.GetStream();
        var line = JsonConvert.SerializeObject(env) + "\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(line), ct);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var readTask = reader.ReadLineAsync();
        if (await Task.WhenAny(readTask, Task.Delay(_config.TimeoutMs, ct)) != readTask)
            throw new NwdGatewayException("TIMEOUT", $"request {command} timed out after {_config.TimeoutMs} ms");
        var response = await readTask ?? throw new NwdGatewayException("TARGET_UNAVAILABLE", "plug-in closed the connection");

        var result = JsonConvert.DeserializeObject<NwdCommandResult>(response)
                     ?? throw new NwdGatewayException("API_ERROR", "unparseable response");
        if (!result.Ok) throw new NwdGatewayException(result.Error?.Code ?? "API_ERROR", result.Error?.Message ?? "unknown error");
        return result.Data ?? JValue.CreateNull();
    }
}
