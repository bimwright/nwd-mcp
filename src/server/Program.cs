using Bimwright.Nwd.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var config = NwdMcpConfig.Load(args);
// stdout is the MCP transport; diagnostics go to stderr.
if (Bimwright.Nwd.Shared.Logging.LogRetentionPolicy.KeepAll)
    System.Console.Error.WriteLine(Bimwright.Nwd.Shared.Logging.LogRetentionPolicy.StartupNotice);
Bimwright.Nwd.Shared.Logging.NwdCallLog.Maintain();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ServerState>();
// Capture the running gateway before initialize/tools/list can be served.
var pluginClient = new PluginClient(config);
builder.Services.AddSingleton(pluginClient);

var responseBudget = ResponseBudget.CreateDefault(
    new ResponseSpillWriter(config.SpillRetentionHours), ResponseSizePolicy.Limits.From(config));
var callLog = new ToolCallLog(config.EnableCallLog);

var mcp = builder.Services
    .AddMcpServer(Program.ConfigureMcpServerOptions)
    .WithStdioServerTransport()
    .WithRequestFilters(f => f.AddCallToolFilter(next => async (request, ct) =>
    {
        // Stamp clientInfo.name on every call so the toast title follows the latest caller (FC-TST-03).
        if (request.Server?.ClientInfo?.Name is { } clientName && !string.IsNullOrWhiteSpace(clientName))
            PluginClient.ClientName = clientName;
        return await callLog.InvokeAsync(request.Params?.Name, request.Params?.Arguments, async () =>
        {
            // Pin the binding generation on arrival (BW-TARGET-001 §6.6) and surface target refusals intact.
            using var targetCall = TargetCallContext.Begin(pluginClient.Generation);
            var result = TargetErrorPayload.Apply(await next(request, ct));
            return ToolErrorFlag.Apply(responseBudget.Apply(request.Params?.Name, request.Params?.Arguments, result));
        });
    }));
mcp = Program.RegisterToolsets(mcp, Program.ResolveToolTypesForRegistration(config), pluginClient.GatewayIdentity, config.ReadOnly);

await builder.Build().RunAsync();
