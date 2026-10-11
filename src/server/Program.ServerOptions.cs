using System.Reflection;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;

internal static partial class Program
{
    internal static readonly string ServerInformationalVersion =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    internal static readonly string ServerVersion = ServerInformationalVersion.Split('+')[0];

    internal static void ConfigureMcpServerOptions(McpServerOptions options)
    {
        options.ServerInstructions = ServerInstructions.Text;
        options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "nwd-mcp", Title = "Navisworks MCP", Version = ServerInformationalVersion,
            Description = "Model Context Protocol gateway for Autodesk Navisworks Manage 2022-2027",
            WebsiteUrl = "https://github.com/bimwright/nwd-mcp"
        };
    }
}
