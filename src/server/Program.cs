using Bimwright.Nwd.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var config = NwdMcpConfig.Load(args);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ServerState>();
// Capture the running gateway before initialize/tools/list can be served.
var pluginClient = new PluginClient(config);
builder.Services.AddSingleton(pluginClient);

var responseBudget = ResponseBudget.CreateDefault();
var callLog = new ToolCallLog(config.EnableCallLog);

var mcp = builder.Services
    .AddMcpServer(Program.ConfigureMcpServerOptions)
    .WithStdioServerTransport()
    .WithRequestFilters(f => f.AddCallToolFilter(next => async (request, ct) =>
        await callLog.InvokeAsync(request.Params?.Name, request.Params?.Arguments, async () =>
            ToolErrorFlag.Apply(responseBudget.Apply(request.Params?.Name, request.Params?.Arguments, await next(request, ct))))));
mcp = Program.RegisterToolsets(mcp, Program.ResolveToolTypesForRegistration(config), pluginClient.GatewayIdentity);

await builder.Build().RunAsync();
