namespace Bimwright.Nwd.Server;

public static class ServerInstructions
{
    public const string Text =
        "nwd-mcp - MCP gateway for Autodesk Navisworks Manage 2022-2027. " +
        "Tools are prefixed nwd_*. Lengths are in the model's display units. " +
        "Multi-instance: if more than one Navisworks may be open, call nwd_list_available_targets " +
        "then nwd_switch_target. Versions are 4-digit years (2022..2027). " +
        "A default launch registers every toolset. " +
        "nwd_list_recent_files reads that year's File menu list. " +
        "nwd_open_file opens a file. nwd_import_model appends a model, or merges it when mode is merge. " +
        "nwd_send_code is ON by default. Turn it off with --disable-send-code " +
        "(or BIMWRIGHT_NWD_ENABLE_SEND_CODE=0) and BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0 " +
        "in the Navisworks process. --read-only also removes it. " +
        SafetyAndPermissions;

    public const string SafetyAndPermissions =
        "Safety & permissions: prefer read tools. " +
        "Before nwd_open_file with discard_changes true, nwd_unhide_all, or any change to a file the user " +
        "did not create in this session, name the exact file or items and get the user's confirmation, " +
        "then pass those literal paths or item ids. " +
        "If a permission check or auto mode denies a call, do not repeat the same action through a baked tool " +
        "or nwd_send_code; report the denial to the user. " +
        "Work on files in the user's project folder and give full paths.";
}
