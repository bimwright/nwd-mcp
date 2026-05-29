using System;
using System.Collections.Generic;
using System.Linq;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
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
builder.Services.AddSingleton<PluginClient>();

builder.Services
    .AddMcpServer(o => o.ServerInstructions = ServerInstructions.Text)
    .WithStdioServerTransport()
    .WithTools(Program.ResolveToolTypesForRegistration(config).ToArray());

await builder.Build().RunAsync();

internal static partial class Program
{
    internal static IReadOnlyList<Type> ResolveToolTypesForRegistration(NwdMcpConfig config)
    {
        var ts = ToolsetFilter.Resolve(config);
        var types = new List<Type>();
        void Add(string toolset, Type t) { if (ts.Contains(toolset)) types.Add(t); }

        Add("meta",            typeof(MetaTools));
        Add("query",           typeof(QueryTools));
        Add("selection",       typeof(SelectionTools));
        Add("selection_write", typeof(SelectionWriteTools));
        Add("sets",            typeof(SetsTools));
        Add("view",            typeof(ViewTools));
        Add("view_write",      typeof(ViewWriteTools));
        Add("visibility",      typeof(VisibilityWriteTools));
        Add("code",            typeof(CodeTools));
        Add("toolbaker",       typeof(ToolBakerTools));
        Add("toolbaker_write", typeof(ToolBakerWriteTools));
        return types;
    }
}
