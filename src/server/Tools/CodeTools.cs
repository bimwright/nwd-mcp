using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class CodeTools
{
    private readonly PluginClient _client;
    public CodeTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_send_code"), Description("Execute a C# code snippet in-process within the Navisworks plug-in context.")]
    public Task<string> SendCode(string source, CancellationToken ct)
        => Call("send_code", new JObject { ["source"] = source }, ct);

    private async Task<string> Call(string command, JObject p, CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync(command, p, ct);
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.Indented);
        }
    }
}
