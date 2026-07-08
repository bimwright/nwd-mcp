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

var mcp = builder.Services
    .AddMcpServer(o => o.ServerInstructions = ServerInstructions.Text)
    .WithStdioServerTransport();
mcp = Program.RegisterToolsets(mcp, Program.ResolveToolTypesForRegistration(config));

await builder.Build().RunAsync();

internal static partial class Program
{
    internal static IMcpServerBuilder RegisterToolsets(IMcpServerBuilder mcp, IEnumerable<Type> toolTypes)
    {
        foreach (var toolType in toolTypes)
        {
            mcp = RegisterToolType(mcp, toolType);
        }

        return mcp;
    }

    private static IMcpServerBuilder RegisterToolType(IMcpServerBuilder mcp, Type toolType)
    {
        if (toolType == typeof(MetaTools)) return mcp.WithTools<MetaTools>();
        if (toolType == typeof(QueryTools)) return mcp.WithTools<QueryTools>();
        if (toolType == typeof(SelectionTools)) return mcp.WithTools<SelectionTools>();
        if (toolType == typeof(SelectionWriteTools)) return mcp.WithTools<SelectionWriteTools>();
        if (toolType == typeof(SetsTools)) return mcp.WithTools<SetsTools>();
        if (toolType == typeof(ViewTools)) return mcp.WithTools<ViewTools>();
        if (toolType == typeof(ViewWriteTools)) return mcp.WithTools<ViewWriteTools>();
        if (toolType == typeof(VisibilityWriteTools)) return mcp.WithTools<VisibilityWriteTools>();
        if (toolType == typeof(CodeTools)) return mcp.WithTools<CodeTools>();
        if (toolType == typeof(ToolBakerTools)) return mcp.WithTools<ToolBakerTools>();
        if (toolType == typeof(ToolBakerWriteTools)) return mcp.WithTools<ToolBakerWriteTools>();

        throw new InvalidOperationException("Unsupported MCP tool type: " + toolType.FullName);
    }

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
