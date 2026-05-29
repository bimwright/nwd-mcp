namespace Bimwright.Nwd.Server;

public static class ServerInstructions
{
    public const string Text =
        "nwd-mcp - MCP gateway for Autodesk Navisworks Manage 2022-2027. " +
        "Tools are prefixed nwd_*. Lengths are in the model's display units. " +
        "Multi-instance: if more than one Navisworks may be open, call nwd_list_available_targets " +
        "then nwd_switch_target. Versions are 4-digit years (2022..2027). " +
        "nwd_send_code is DISABLED unless the server is started with --enable-send-code " +
        "(or BIMWRIGHT_NWD_ENABLE_SEND_CODE=1) AND the plug-in opts in via " +
        "BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1.";
}
