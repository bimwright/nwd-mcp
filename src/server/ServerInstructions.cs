namespace Bimwright.Nwd.Server;

// INSTR-MAX-2KB: Text stays within 2,048 UTF-8 bytes so every client reads it whole, including the
// Safety & permissions block at the end (FC-SRV-06). Detail that belongs to one tool lives in that
// tool's description. Order: product keywords, target -> health -> report, tool index, safety.
public static class ServerInstructions
{
    public const string Text =
        "nwd-mcp - MCP gateway for Autodesk Navisworks Manage 2022-2027 (.nwd/.nwf/.nwc): model tree, properties, " +
        "search, sets, selection, viewpoints, visibility. Tools are nwd_*; lengths are in the model's display units. " +
        "Use only registered tools. Binding never moves on its own. List via nwd_list_available_targets if available, then pin via nwd_switch_target(\"pid:<n>\"). " +
        "If switching is unavailable, start reads with --target pid:<n>. Then, if available, call nwd_health_check(check_updates=true) once. " +
        "For writes, include meta in writable mode; confirm the file with user and switch with verify=true. " +
        "AMBIGUOUS_TARGET, NO_TARGET, PAIRING_REQUIRED, DOCUMENT_CHANGED and TARGET_* except TARGET_INTERRUPTED: not sent/started. " +
        "HOST_BUSY: not started, won't run later; ask user and wait until idle (check health if available). " +
        "TIMEOUT and TARGET_INTERRUPTED mean outcome unknown: inspect the document before retrying a write. " +
        "If available, call nwd_report_task_result once before replying for document changes or issues to check, not chat-only. " +
        "Tools: targets/health, files, query, selection, sets, viewpoints, visibility, send_code, baked tools, reports. " +
        "All toolsets by default; --read-only hides writes. " +
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
