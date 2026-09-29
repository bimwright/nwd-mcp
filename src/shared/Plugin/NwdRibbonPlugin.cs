using Autodesk.Navisworks.Api.Plugins;

namespace Bimwright.Nwd.Shared.Plugin;

[Plugin("Bimwright.Nwd.Ribbon", "BMWR", DisplayName = "Bimwright", ToolTip = "Activity toast for nwd-mcp")]
[RibbonLayout("NwdRibbon.xaml")]
[RibbonTab("ID_Bimwright_Nwd_Tab", DisplayName = "Bimwright", LoadForCanExecute = true)]
[Command("ID_Bimwright_Nwd_Toasts", DisplayName = "Toasts", CanToggle = true,
    CallCanExecute = CallCanExecute.Always, LoadForCanExecute = true,
    ToolTip = "Show or hide activity toasts")]
[Command("ID_Bimwright_Nwd_ToastBrand", DisplayName = "Toast Brand", CanToggle = true,
    CallCanExecute = CallCanExecute.Always, LoadForCanExecute = true,
    ToolTip = "Show the BIMwright wordmark when the pointer is on the card")]
[Command("ID_Bimwright_Nwd_Status", DisplayName = "Status",
    LoadForCanExecute = true, ToolTip = "Activity toast settings")]
[AddInPlugin(AddInLocation.None)]
public sealed class NwdRibbonPlugin : CommandHandlerPlugin
{
    private const string Toasts = "ID_Bimwright_Nwd_Toasts";
    private const string Brand = "ID_Bimwright_Nwd_ToastBrand";
    private const string Status = "ID_Bimwright_Nwd_Status";

    public override int ExecuteCommand(string name, params string[] parameters)
    {
        switch (name)
        {
            case Toasts:
                NwdActivityToast.ToggleEnabled();
                break;
            case Brand:
                NwdActivityToast.ToggleBranding();
                break;
            case Status:
                NwdActivityToast.ShowStatusWindow();
                break;
        }
        return 0;
    }

    public override CommandState CanExecuteCommand(string commandId)
    {
        var state = new CommandState(true);
        switch (commandId)
        {
            case Toasts:
                state.IsChecked = NwdActivityToast.Enabled;
                break;
            case Brand:
                state.IsEnabled = NwdActivityToast.Enabled;
                state.IsChecked = NwdActivityToast.ShowBranding;
                break;
        }
        return state;
    }

    public override bool CanExecuteRibbonTab(string tabId)
    {
        return true;
    }
}
