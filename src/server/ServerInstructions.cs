namespace Bimwright.Nwd.Server;

// INSTR-MAX-2KB: Text stays within 2,048 UTF-8 bytes so every client reads it whole, including the
// Safety & permissions block at the end (FC-SRV-06). Detail that belongs to one tool lives in that
// tool's description. Order: product keywords, target -> health -> report, tool index, safety.
public static class ServerInstructions
{
    public const string Text =
        "nwd-mcp - MCP gateway for Autodesk Navisworks Manage 2022-2027 (.nwd/.nwf/.nwc): model tree, properties, " +
        "search, sets, selection, viewpoints, visibility. Tools are nwd_*; lengths are in the model's display units. " +
        "Start: the binding never moves on its own. If several may be open or several agents run, " +
        "pin nwd_switch_target(\"pid:<n>\") from nwd_list_available_targets, then call nwd_health_check(check_updates=true) once " +
        "and report it. Writes need a confirmed document: after the user confirms the file, call nwd_switch_target(\"pid:<n>\", verify=true). " +
        "AMBIGUOUS_TARGET, NO_TARGET, PAIRING_REQUIRED, DOCUMENT_CHANGED and TARGET_* except TARGET_INTERRUPTED mean not sent " +
        "(see nwd_switch_target). HOST_BUSY means not started and will not run later (dialog open or UI busy): ask the user, then resend. " +
        "TIMEOUT and TARGET_INTERRUPTED mean outcome unknown: inspect the document before retrying a write. " +
        "After work that changed a document or found issues, call nwd_report_task_result once before your final answer " +
        "(not for chat-only answers). " +
        "Tools: targets/health, files, query, selection, sets, viewpoints, visibility, send_code, baked tools, reports. " +
        "Default: every toolset; --read-only keeps read tools. " +
        "Results over 64 KiB carry _response_warning (budget 1 MiB); tools with an output parameter can spill to a file. " +
        "nwd_send_code is ON by default; off: server --disable-send-code or BIMWRIGHT_NWD_ENABLE_SEND_CODE=0, or " +
        "BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0 in Navisworks; --read-only removes it. " +
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
