using System;
using System.Collections.Generic;
using System.Reflection;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

internal static partial class Program
{
    internal static readonly (string Name, Type ToolType,
        Func<IMcpServerBuilder, Newtonsoft.Json.Linq.JObject?, IMcpServerBuilder> Register)[] ToolsetRegistrations =
    {
        ("meta", typeof(MetaTools), static (mcp, _) => mcp.WithTools<MetaTools>()),
        ("files", typeof(FileTools), static (mcp, _) => mcp.WithTools<FileTools>()),
        ("files_write", typeof(FileWriteTools), static (mcp, _) => mcp.WithTools<FileWriteTools>()),
        ("query", typeof(QueryTools), RegisterQueryToolsWithHealthMeta),
        ("selection", typeof(SelectionTools), static (mcp, _) => mcp.WithTools<SelectionTools>()),
        ("selection_write", typeof(SelectionWriteTools), static (mcp, _) => mcp.WithTools<SelectionWriteTools>()),
        ("sets", typeof(SetsTools), static (mcp, _) => mcp.WithTools<SetsTools>()),
        ("view", typeof(ViewTools), static (mcp, _) => mcp.WithTools<ViewTools>()),
        ("view_write", typeof(ViewWriteTools), static (mcp, _) => mcp.WithTools<ViewWriteTools>()),
        ("visibility", typeof(VisibilityWriteTools), static (mcp, _) => mcp.WithTools<VisibilityWriteTools>()),
        ("code", typeof(CodeTools), static (mcp, _) => mcp.WithTools<CodeTools>()),
        ("toolbaker", typeof(ToolBakerTools), static (mcp, _) => mcp.WithTools<ToolBakerTools>()),
        ("toolbaker_write", typeof(ToolBakerWriteTools), static (mcp, _) => mcp.WithTools<ToolBakerWriteTools>())
    };

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
        foreach (var registration in ToolsetRegistrations)
            if (registration.ToolType == toolType)
                return registration.Register(mcp, gatewayIdentity);

        throw new InvalidOperationException("Unsupported MCP tool type: " + toolType.FullName);
    }

    private static IMcpServerBuilder RegisterQueryToolsWithHealthMeta(IMcpServerBuilder mcp,
        Newtonsoft.Json.Linq.JObject? gatewayIdentity)
    {
        var toolType = typeof(QueryTools);
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

    internal static IReadOnlyList<Type> ResolveToolTypesForRegistration(NwdMcpConfig config)
    {
        var ts = ToolsetFilter.Resolve(config);
        var types = new List<Type>();
        foreach (var registration in ToolsetRegistrations)
            if (ts.Contains(registration.Name))
                types.Add(registration.ToolType);
        return types;
    }
}
