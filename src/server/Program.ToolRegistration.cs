using System;
using System.Collections.Generic;
using System.Linq;
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
        Newtonsoft.Json.Linq.JObject? gatewayIdentity = null, bool readOnly = false)
    {
        foreach (var toolType in toolTypes)
        {
            mcp = readOnly
                ? RegisterMethods(mcp, toolType, ToolMethods(toolType).Where(IsReadOnlyTool), gatewayIdentity)
                : RegisterToolType(mcp, toolType, gatewayIdentity);
        }

        return mcp;
    }

    /// <summary>
    /// Every tool method that registers for <paramref name="config"/>: the toolset filter, then under --read-only only
    /// tools annotated ReadOnly = true (family contract D10/D11, BW-CK Tool-Annotation: the read-only list equals the
    /// ReadOnly set). Transient UI tools (goto viewpoint, selection) stay; nwd_switch_target and send_code do not.
    /// </summary>
    internal static IReadOnlyList<MethodInfo> ResolveToolMethodsForRegistration(NwdMcpConfig config)
        => ResolveToolTypesForRegistration(config)
            .SelectMany(ToolMethods)
            .Where(m => !config.ReadOnly || IsReadOnlyTool(m))
            .ToList();

    internal static IEnumerable<MethodInfo> ToolMethods(Type toolType)
        => toolType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

    internal static bool IsReadOnlyTool(MethodInfo method)
        => method.GetCustomAttribute<McpServerToolAttribute>()?.ReadOnly == true;

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
        => RegisterMethods(mcp, typeof(QueryTools), ToolMethods(typeof(QueryTools)), gatewayIdentity);

    private static IMcpServerBuilder RegisterMethods(IMcpServerBuilder mcp, Type toolType, IEnumerable<MethodInfo> methods,
        Newtonsoft.Json.Linq.JObject? gatewayIdentity)
    {
        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttribute<McpServerToolAttribute>()!;
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
