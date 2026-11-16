namespace Bimwright.Nwd.Server;

public static class ServerInstructions
{
    public const string Text =
        "nwd-mcp - MCP gateway for Autodesk Navisworks Manage 2022-2027. " +
        "Tools are prefixed nwd_*. Lengths are in the model's display units. " +
        "Targeting: the gateway binds to one Navisworks (PID + process start) and never moves on its own. " +
        "If several may be open or several agents run, call nwd_list_available_targets (pid, window_title) and pin " +
        "nwd_switch_target(\"pid:<n>\") at session start; list/get never select. Selectors: pid:<n>, id:<target_id>, 2022..2027, auto; never R-codes. " +
        "AMBIGUOUS_TARGET, TARGET_LOST, TARGET_CHANGED, TARGET_UNAVAILABLE and NO_TARGET mean the command was not sent: inspect error.target.candidates, " +
        "confirm with nwd_switch_target(\"pid:<n>\"), then call again. TARGET_REBIND_REQUIRED: a year/auto selector cannot move a binding; " +
        "leaving a live explicit choice needs rebind=true. TARGET_INTERRUPTED: the command was sent and its outcome is unknown; " +
        "inspect the document before retrying a write. " +
        "Writes need a confirmed document: after the user confirms the reported file, call nwd_switch_target(\"pid:<n>\", verify=true) once. " +
        "TARGET_UNCONFIRMED and PAIRING_REQUIRED mean not sent; DOCUMENT_CHANGED means another file is open now and the command did not start: " +
        "ask the user, then verify again. nwd_open_file moves the confirmation to the file it opens. " +
        "HOST_BUSY means Navisworks could not start the command (a modal dialog waits for the user, or its UI thread stayed busy): " +
        "it was not started and will not run later; ask the user to answer the dialog, check nwd_health_check host_busy, then send it again. " +
        "TIMEOUT means the command started and its outcome is unknown: inspect before retrying a write. " +
        "After Navisworks work that changed a document or found something the user should check, call nwd_report_task_result once " +
        "before your final answer (not for chat-only answers); responses of document writes carry _report until you do. " +
        "In nwd_send_code put values that change from run to run in data and read them as data[\"key\"]; " +
        "result_contract=\"task_v1\" makes the script return McpTaskResult.Pass(value) or McpTaskResult.Fail(code, message). " +
        "At session start, select the intended target then call nwd_health_check(check_updates=true) once to discover newer stable releases " +
        "and report host state, loaded server/plugin versions, build ids and alignment. If network access is unavailable or disallowed, " +
        "use the offline default and keep the update status not_checked/unknown. Later routine probes stay offline. " +
        "Inspect both update.components; missing evidence or a failed lookup is unknown, not up to date. " +
        "When update.notification is present, report its code/message and follow agent_action: propose the release ZIP only if " +
        "bundle_update_proposal_allowed=true and update_guidance.mode=prepare_update; other modes contain only check/review steps. " +
        "UPDATE_CHECK_UNAVAILABLE reports an external lookup_reason, not a runtime fault or request for installation authorization. " +
        "Report LOCAL_VERSION_AHEAD neutrally; preserve protected_components and explain protected_component_reasons. " +
        "unobserved_components need connection/runtime observation, not a corruption warning. A review warning does not mean a new version exists. " +
        "Installation authorization applies only to a permitted proposal; reuse existing permission. Never recommend a downgrade or " +
        "automatically replace a development build, install or restart processes. host_connected=false or ok=false is not a successful host connection. " +
        "Matching revisions do not verify artifact bytes, compatibility or publisher trust. " +
        "After updating and reconnecting, compare fresh initialize serverInfo and tools/list metadata/schema/annotations " +
        "on that same connection with health.client_verification. The server cannot observe client cache refresh. " +
        "A default launch registers every toolset. " +
        "nwd_list_recent_files reads that year's File menu list. " +
        "nwd_open_file opens a file. nwd_import_model appends a model, or merges it when mode is merge. " +
        "nwd_send_code is ON by default. Turn it off with --disable-send-code " +
        "(or BIMWRIGHT_NWD_ENABLE_SEND_CODE=0) and BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0 " +
        "in the Navisworks process. --read-only also removes it. " +
        "Large results: a _response_warning field marks results over 64 KiB and the budget is 1 MiB. " +
        "nwd_get_model_tree, nwd_batch_get_properties, nwd_find_items_by_name and nwd_run_baked_tool " +
        "accept output=file to write the full result to a local file and return its path, schema and a preview; " +
        "nwd_send_code output over 1 MiB auto-spills the same way. " +
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
