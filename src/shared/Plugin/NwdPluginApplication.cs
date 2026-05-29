using System;
using System.IO;
using Bimwright.Nwd.Shared.Transport;
using Bimwright.Nwd.Shared.Infrastructure;
using NW = Autodesk.Navisworks.Api;
using NWP = Autodesk.Navisworks.Api.Plugins;

namespace Bimwright.Nwd.Shared.Plugin;

[NWP.Plugin("Bimwright.Nwd.Plugin", "BMWR", DisplayName = "Bimwright Navisworks MCP", ToolTip = "Bimwright MCP gateway for Autodesk Navisworks Manage")]
public sealed class NwdPluginApplication : NWP.EventWatcherPlugin
{
    private static TcpTransportServer? _server;

    public override void OnLoaded()
    {
        // Fail-safe check
        try
        {
            if (NW.Application.HostProduct != NW.HostProduct.Manage)
            {
                return;
            }
        }
        catch
        {
            // Outside Navisworks
            return;
        }

        var year = 2026;
#if NAVIS2022
        year = 2022;
#elif NAVIS2023
        year = 2023;
#elif NAVIS2024
        year = 2024;
#elif NAVIS2025
        year = 2025;
#elif NAVIS2026
        year = 2026;
#elif NAVIS2027
        year = 2027;
#endif

        var enableSendCodeEnv = Environment.GetEnvironmentVariable("BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE");
        var enableSendCode = !string.IsNullOrEmpty(enableSendCodeEnv) && 
            (enableSendCodeEnv.Equals("1", StringComparison.OrdinalIgnoreCase) || 
             enableSendCodeEnv.Equals("true", StringComparison.OrdinalIgnoreCase));

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var descriptorDir = Path.Combine(appData, "Bimwright", "nwd-mcp");

        var options = new PluginOptions(year, enableSendCode, 0);
        _server = new TcpTransportServer(options, descriptorDir);

        var handlers = NwdCommandRegistry.Build(options);
        _server.Start(handlers);
    }

    public override void OnUnloading()
    {
        _server?.Dispose();
        _server = null;
    }
}
