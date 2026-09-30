using System;
using System.IO;
using Bimwright.Nwd.Shared.Config;
using Bimwright.Nwd.Shared.Logging;
using Bimwright.Nwd.Shared.Transport;
using Bimwright.Nwd.Shared.Infrastructure;
using NW = Autodesk.Navisworks.Api;
using NWP = Autodesk.Navisworks.Api.Plugins;

namespace Bimwright.Nwd.Shared.Plugin;

[NWP.Plugin("Bimwright.Nwd.Plugin", "BMWR", DisplayName = "Bimwright Navisworks MCP", ToolTip = "Bimwright MCP gateway for Autodesk Navisworks Manage")]
public sealed class NwdPluginApplication : NWP.EventWatcherPlugin
{
    private static TcpTransportServer? _server;
    private static bool _idleHooked;

    public override void OnLoaded()
    {
        NavisworksUiThreadInvoker.Capture();
        // Manage-only product: ApplicationPlugins RuntimeRequirements already
        // gates Platform=NAVMAN. HostProduct was removed from the public API in
        // recent Navisworks releases, so do not probe it here.

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

        var enableSendCode = PluginSendCodeEnabled();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var descriptorDir = Path.Combine(appData, "Bimwright", "nwd-mcp");

        var options = new PluginOptions(year, enableSendCode, 0);
        _server = new TcpTransportServer(options, descriptorDir);
        NwdCallLog.SetEnabled(PluginSettings.LoadRecordCalls());
        NwdActivityToast.Attach(_server, year);

        var handlers = NwdCommandRegistry.Build(options);
        _server.Start(handlers);

        if (!_idleHooked)
        {
            NW.Application.Idle += OnIdleToast;
            NW.Application.GuiCreated += OnGuiCreated;
            _idleHooked = true;
        }
    }

    public override void OnUnloading()
    {
        if (_idleHooked)
        {
            NW.Application.Idle -= OnIdleToast;
            NW.Application.GuiCreated -= OnGuiCreated;
            _idleHooked = false;
        }
        NwdActivityToast.Stop();
        _server?.Dispose();
        _server = null;
    }

    /// <summary>
    /// Unset means on. 0/false/no/off, or any other value, means off.
    /// </summary>
    private static bool PluginSendCodeEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE");
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                return true;
            default:
                return false;
        }
    }

    private static void OnIdleToast(object sender, EventArgs e)
    {
        NavisworksUiThreadInvoker.Capture();
        NwdActivityToast.OnIdle();
    }

    private static void OnGuiCreated(object sender, EventArgs e)
    {
        NavisworksUiThreadInvoker.Capture();
        NwdActivityToast.OnIdle();
    }
}
