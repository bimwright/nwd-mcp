using System;
using System.Collections.Generic;
using System.Reflection;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

internal static partial class Program
{
    internal static IMcpServerBuilder RegisterToolsets(IMcpServerBuilder mcp, IEnumerable<Type> toolTypes,
        Newtonsoft.Json.Linq.JObject? gatewayIdentity = null)
    {
        foreach (var toolType in toolTypes)
        {
            mcp = RegisterToolType(mcp, toolType, gatewayIdentity);
        }

        return mcp;
    }

    private static IMcpServerBuilder RegisterToolType(IMcpServerBuilder mcp, Type toolType,
        Newtonsoft.Json.Linq.JObject? gatewayIdentity)
    {
        if (toolType == typeof(MetaTools)) return mcp.WithTools<MetaTools>();
        if (toolType == typeof(FileTools)) return mcp.WithTools<FileTools>();
        if (toolType == typeof(FileWriteTools)) return mcp.WithTools<FileWriteTools>();
        if (toolType == typeof(QueryTools))
        {
            foreach (var method in toolType.GetMethods())
            {
                var attribute = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attribute == null) continue;
                var tool = McpServerTool.Create(method,
                    context => ActivatorUtilities.CreateInstance(context.Services!, toolType));
                if (attribute.Name == "nwd_health_check" && gatewayIdentity is not null)
                    tool.ProtocolTool.Meta = new System.Text.Json.Nodes.JsonObject
                    {
                        [HealthRuntimeInfo.ClientRuntimeMetadataKey] = System.Text.Json.Nodes.JsonNode.Parse(
                            HealthRuntimeInfo.ClientRuntime(gatewayIdentity).ToString()),
                    };
                mcp.WithTools(new[] { tool });
            }
            return mcp;
        }
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
        Add("files",           typeof(FileTools));
        Add("files_write",     typeof(FileWriteTools));
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
