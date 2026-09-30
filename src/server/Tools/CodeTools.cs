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

    [McpServerTool(Name = "nwd_send_code"), Description("Run a C# script inside Navisworks on its UI thread. doc is the active Document; the value of the last expression comes back as result and Console output as stdout. Navisworks is blocked while the script runs, so keep it short and do not await. Output above 1 MiB auto-spills to a local same-machine file with schema and preview; there is no output parameter.")]
    public Task<string> SendCode(string code, CancellationToken ct)
        => Call("send_code", new JObject { ["code"] = code }, ct);

    private async Task<string> Call(string command, JObject p, CancellationToken ct)
    {
        try
        {
            var data = await _client.SendAsync(command, p, ct);
            return JsonConvert.SerializeObject(data, Formatting.None);
        }
        catch (NwdGatewayException ex)
        {
            return JsonConvert.SerializeObject(new { ok = false, error = new { code = ex.Code, message = ex.Message } }, Formatting.None);
        }
    }
}
