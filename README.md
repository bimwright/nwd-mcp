<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#capabilities--architecture"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#tool-surface"><img src="https://img.shields.io/badge/MCP-33%20tools-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  English · <a href="README.vi.md">Tiếng Việt</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

`nwd-mcp` is a professional-grade Model Context Protocol (MCP) gateway for **Autodesk Navisworks Manage** automation. It enables AI agents to query, inspect, navigate, and script Autodesk Navisworks Manage desktop sessions locally over stdin/stdout.

---

## Capabilities & Architecture
- **Supported Host:** Autodesk Navisworks Manage only. (Freedom and Simulate are not supported).
- **Supported Versions:** 2022, 2023, 2024, 2025, 2026, and 2027.
- **Two-Process Model:** Lightweight `.NET 8` console server communicating with a version-specific in-process plug-in. Wire transport is **TCP NDJSON over loopback** for every supported year (2022–2027); all six plug-in shells target `.NET Framework 4.8` / `net48`. (Unlike the rest of the bimwright family, nwd-mcp does **not** use Named Pipe transport — the loopback-TCP convention applies uniformly across all versions.)
- **Security First:** Per-session random cryptographic token validation, loopback-only binding for the TCP transport, and absolute file path sanitization in error messages returned to the model.
- **Multi-Instance Routing:** The gateway binds to one Navisworks instance, identified by PID and process start time, and never moves to another one on its own. With several matching instances and no choice yet, commands return `AMBIGUOUS_TARGET`. If the bound instance exits, commands return `TARGET_LOST` or `TARGET_CHANGED` (not sent) until you confirm a target with `nwd_switch_target`. Selectors are `pid:<n>`, `id:<target_id>`, a year 2022–2027, or `auto`. When several agents or same-year instances run, call `nwd_list_available_targets` (it shows `pid` and `window_title`) and pin `nwd_switch_target("pid:<n>")` at session start. A year or `auto` never moves an existing binding (`TARGET_REBIND_REQUIRED`), and leaving a live, explicitly chosen instance needs `rebind=true`. `nwd_list_available_targets` and `nwd_get_current_target` never select a target. Writes run only on a confirmed document: check with the user that the reported file is the one to change, then call `nwd_switch_target("pid:<n>", verify=true)` once. Until then writes return `TARGET_UNCONFIRMED`; if another file was opened since, `DOCUMENT_CHANGED` (the command did not start). `nwd_open_file` moves the confirmation to the file it opens. A server and plug-in from different releases return `PAIRING_REQUIRED` for writes; reads still work.
- **Busy Navisworks:** when a modal dialog waits for the user, or the UI thread stays busy until the command's start deadline, the command returns `HOST_BUSY`: it was not started and will not run later, so it is safe to send again once Navisworks is free. `TIMEOUT` now means the command started and its outcome is unknown: inspect before retrying a write. `nwd_health_check` still answers while Navisworks is busy, with `host_busy` and `busy_reason` (`modal_dialog` or `ui_thread_busy`).

Navisworks shows one activity card for MCP commands. Toasts are on by default and stay up for 20 seconds after the latest result (10, 20, 30, or 60). Hover pauses the card; leaving it starts the selected interval again. A deliberate hover smoothly expands a compact three-row view of the current card's individually bordered outcomes (tool, result, success/failure and local completion time in 24-hour `HH:mm:ss`). Every outcome in that card is retained, oldest above newest; the view starts at the latest three, and scrolling up reaches earlier calls through a thin RVT-style scrollbar. Leaving smoothly collapses it. Incoming outcomes appear immediately: at the bottom, the view follows the newest calls; while reading older calls, it preserves your scroll position. Returning to the bottom resumes following. The preview works with branding off and respects reduced motion. These length-limited summaries live only in memory and are discarded when the card closes or resets; they do not enable Record or retain raw requests, response JSON or script source. The BIMwright wordmark stays off unless Show branding is turned on. `nwd_health_check` does not appear on the card. The **Bimwright** ribbon tab has **Toasts**, **Toast Brand**, **Record**, and **Status**. **Record** supplies the host default for legacy gateways; current gateways use the server call-log setting described below. `send_code` is stored as a length and SHA-256, not the script. Settings live in `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json`. `BIMWRIGHT_NWD_ENABLE_TOAST` and `BIMWRIGHT_NWD_RECORD_CALLS` override those switches at the next launch.

The card title names the Navisworks year and the agent of the latest call (for example `nwd-mcp 2025 · Claude`); the Navisworks process ID (`· PID 12345`) fades in when you point at the card, with or without branding. An older gateway sends no agent name, so the title shows none. **Pause for today**, at the foot of the expanded card or in **Status**, silences every card, errors included, in every Navisworks on this computer until local midnight. On the card it turns into **Resume**, so a mis-click is undone at once; **Toasts** on the ribbon also ends the pause, and so does turning toasts on. The pause is saved as the local date in `toastSnoozedDate`; while it lasts, `nwd_report_task_result` returns `toast_shown: false`. Update the gateway and plug-in together.

**Status → toast position:** choose horizontal Left/Right and vertical Top/Bottom independently (default: top-left). Changes apply and save immediately; changing the corner clears any saved drag offset. Enable **Allow dragging the toast** to drag its title row; a normal click still dismisses the card. Disabling drag returns to the selected corner without deleting the saved position; re-enabling restores it. **Reset position** removes the offset. Positions are stored relative to the Navisworks window in DIP and clamped to its monitor's work area. Bottom anchors expand upward; if space is insufficient, expansion uses the opposite side. Motion respects the Windows animation setting. A save failure is reported inline in Status while the new preference remains active for this session. Latest-tool and outcome tooltips include the sanitized result, status, completion time and measured duration when available. This does not coordinate overlapping toasts from different apps.

---

## Current Status

| Component | Status |
|---|---|
| MCP gateway server (.NET 8) | ✅ Builds warning-clean (Debug + Release) |
| Unit tests (155 xUnit) | ✅ All passing |
| Plug-in handler implementations | ✅ Verified against a live Navisworks Manage session |
| Plug-in projects (net48) | ✅ Compile against the Navisworks Manage SDK |

> **Note:** The plug-in handler layer uses real Navisworks .NET API calls (not stubs or fabricated
> data) and has been exercised against a live Navisworks Manage instance.
> The first-run checklist is [docs/testing/manual-smoke.md](docs/testing/manual-smoke.md).

---

## Install

Download the client setup ZIP from [GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest). It includes a self-contained MCP server and the Navisworks Manage plugin years compiled for that release (see `manifest.json`). The v1.0.0 ZIP ships **2025** only. Other years: build from source against that year’s Manage SDK.

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/nwd-mcp/releases/latest).tag_name
$zip = "$env:TEMP\NwdMcp.Setup-$tag-win-x64.zip"
$dir = "$env:TEMP\NwdMcp.Setup-$tag-win-x64"
Invoke-WebRequest "https://github.com/bimwright/nwd-mcp/releases/download/$tag/NwdMcp.Setup-$tag-win-x64.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force

powershell -ExecutionPolicy Bypass -File "$dir\install.ps1" -WhatIf
powershell -ExecutionPolicy Bypass -File "$dir\install.ps1"
```

Deploys `%APPDATA%\Autodesk\ApplicationPlugins\Bimwright.Nwd.bundle\` and `nwd-mcp.exe` at the fixed path `%LOCALAPPDATA%\Bimwright\nwd-mcp\server\current\`. Restart Navisworks Manage. Point your MCP client at that `nwd-mcp.exe` path. Pin a year with `--target 2025` or `BIMWRIGHT_NWD_TARGET=2025` when multiple instances may run.

The NuGet package `Bimwright.Nwd.Server` is server-only: it has no Navisworks plug-in. Install from the setup ZIP; use `dotnet tool install -g Bimwright.Nwd.Server` only if the plug-in is already installed from the ZIP and you want the server as a global .NET tool (command `bimwright-nwd`).

**Developer:** `dotnet build` a `plugin-navisNN` project, then `pwsh scripts\install-bundle.ps1 -Year 2025 -Configuration Release`.

Useful flags after wiring: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`.

---

## Tool Surface

A default launch registers every toolset: **35 tools**. `--read-only` leaves **25**. Every tool uses the `nwd_*` prefix.

### 1. Target/Meta Tools (5)
* `nwd_list_available_targets` — List all active discovered Navisworks sessions.
* `nwd_get_current_target` — Report which session the server is currently pointed at.
* `nwd_switch_target` — Point the gateway at a different active session; `verify=true` also confirms the open document for writes.
* `nwd_report_task_result` — Report a finished job once (completed, failed or cancelled, one-line summary, optional Markdown body): a card on the Navisworks toast and a saved report. Agent-reported, not verified. Responses of document writes carry `_report` until you do.
* `nwd_get_report_history` — Read saved reports from `%LOCALAPPDATA%\Bimwright\nwd-mcp\reports`, newest first, or one in full by `report_id`.

### 2. File Tools (3)
* `nwd_list_recent_files` — List recent files for the running Manage year, in File menu order.
* `nwd_open_file` *(Write)* — Open a file. Refuses while the current file has unsaved changes unless `discard_changes` is true.
* `nwd_import_model` *(Write)* — Bring a model into the open document. `append` (default) adds it; `merge` combines it.

### 3. Query/Read Tools (8)
* `nwd_health_check` — Probe host/document state and loaded server/plugin versions, build ids and alignment. Offline by default; optional `check_updates=true` compares both components with the latest official stable release and tag revision. No host still returns server identity with `ok:false`, `host_connected:false`.
* `nwd_get_document_info` — Retrieve active document name, file path, and model count.
* `nwd_get_model_statistics` — Retrieve counts of elements, models, and selections.
* `nwd_get_model_tree` — Retrieve a bounded model tree node hierarchy.
* `nwd_get_item_properties` — Retrieve property categories and property lists for an element.
* `nwd_batch_get_properties` — Retrieve properties for multiple elements at once.
* `nwd_find_items` — Query elements using advanced property/category filters.
* `nwd_find_items_by_name` — Search elements by display name.

### 4. Selection Tools (3)
* `nwd_get_current_selection` — Retrieve element IDs of the active user selection.
* `nwd_clear_selection` *(Write)* — Clear the active selection.
* `nwd_select_items_by_search` *(Write)* — Select items matching property/name filters.

### 5. Selection Sets Tools (3)
* `nwd_list_sets` — Retrieve selection and search sets, recursing folders.
* `nwd_get_selection_set_items` — Retrieve elements inside a set.
* `nwd_execute_search_set` *(Mixed)* — Execute a search set; optionally select matches.

### 6. Viewpoint/Navigation Tools (4)
* `nwd_list_viewpoints` — Enumerate saved viewpoints and folders.
* `nwd_get_current_viewpoint` — Retrieve current camera and view state.
* `nwd_goto_viewpoint` *(Write)* — Navigate the viewport camera to a saved viewpoint.
* `nwd_save_viewpoint` *(Write)* — Save the active view as a named viewpoint.

### 7. Visibility Tools (2)
* `nwd_hide_items` *(Write)* — Hide/show specified elements.
* `nwd_unhide_all` *(Write)* — Reset all hidden elements to visible.

### 8. Escape Hatch Scripting (1)
* `nwd_send_code` *(Write)* — Run a C# script on the Navisworks UI thread with `doc` as the active document. Returns the last expression as `result` and Console output as `stdout`; Navisworks waits while it runs.

### 9. ToolBaker Governed Tools (6)
* `nwd_list_baked_tools` — List all verified compiled reusable tools.
* `nwd_run_baked_tool` *(Write)* — Run an accepted baked tool by name with parameters.
* `nwd_list_bake_suggestions` — List adaptive workflow suggestions.
* `nwd_accept_bake_suggestion` *(Write)* — Validate, compile, and deploy a suggested tool.
* `nwd_dismiss_bake_suggestion` *(Write)* — Dismiss/snooze an active suggestion.
* `nwd_create_bake_issue_draft` — Generate a draft GitHub issue for requested tools.

---

## Runtime health and updates

Start each Navisworks session with one `nwd_health_check(check_updates=true)` call after selecting the target, so the agent discovers newer stable releases. If network access is unavailable or disallowed, use the offline default and leave update status not checked/unknown. Later routine probes stay offline. `setup_gateway`/`setup_plugin` describe loaded assemblies; their SHA-256 observes the disk file at capture time, not loaded memory. Version/revision alignment is not proof of compatibility or publisher trust. Legacy plugins without identity report unknown version/build evidence. The host probe uses the UI thread and can time out while Navisworks is busy.

`check_updates=true` uses one official release baseline and a shared 5-second network budget. Inspect both `update.components.server` and `.plugin`: matching versions alone are insufficient, and unavailable evidence remains `unknown`. `checked` confirms release lookup, not that both components are current. Per-component `update_available` compares semantic versions; the overall flag is false only when both versions/revisions match, true when a newer stable version exists for either component, and null otherwise. Source-revision comparison does not verify artifact bytes.

Inspect `update.notification` and follow `agent_action`. Codes distinguish `NEW_RELEASE_AVAILABLE`, `RELEASE_REVIEW_REQUIRED`, `RELEASE_IDENTITY_UNVERIFIED`, `UPDATE_CHECK_UNAVAILABLE` and informational `LOCAL_VERSION_AHEAD`. External lookup failures report `lookup_reason` without claiming a local runtime fault or requesting installation authorization. Preserve `protected_components` with their `protected_component_reasons`; a newer local component must not be downgraded. `unobserved_components` need connection/runtime evidence, not an assumption that the host is closed or a known build is corrupt. `identity_observed` and `comparison_reason` distinguish missing runtime metadata from unavailable release evidence. Null notification and per-component semantic `update_available=false` do not prove release identity.

`update_guidance.mode` is `check_only`, `retry_check`, `review`, `none` or `prepare_update`. Only `prepare_update`, with `bundle_update_proposal_allowed=true`, returns installation steps and requires authorization (reuse existing permission). Other modes contain only check/report/review actions. A newer local component with an older peer, unresolved peer build, failed tag lookup or unobserved peer blocks the full ZIP proposal. Health never downloads packages, installs, saves/closes documents or restarts anything. Follow [AGENTS.md](AGENTS.md) for preview, authorization, backups and verification; the current installer does not provide atomic bundle+server rollback. Do not automatically replace development builds.

After restarting Navisworks and reconnecting the MCP client, compare `initialize.serverInfo` and fresh `tools/list` runtime metadata/schema/annotations on that same connection with health's `client_verification`. The server cannot prove the client refreshed its cache; absent client evidence stays unverified.

## Safety Configurations

### Read-Only Mode
Strict read-only mode can be enforced via the `--read-only` flag or the `BIMWRIGHT_NWD_READ_ONLY=1` environment variable.
- Only tools annotated read-only register. Three transient UI tools stay: `nwd_goto_viewpoint`, `nwd_clear_selection` and `nwd_select_items_by_search` (they change the view or selection, not the model). `nwd_switch_target` and every write are removed; pin the target at startup with `--target pid:<n>`.
- `nwd_execute_search_set(select=true)` still selects the matches: like the transient UI tools above, it changes the selection, not the model.
- The total read-only tool surface is exactly **25 tools**. `nwd_list_recent_files` stays; `nwd_open_file` and `nwd_import_model` do not.

### Tool-call record
Call logging is **off by default**. `--enable-call-log` records every MCP tool call, including local tools and failures, in `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwd-mcp-calls.jsonl`; forwarded commands also record native outcomes in `mcp-calls.jsonl` in the same directory. `--disable-call-log` prevents creation or appending of both logs for this gateway. The MCPB `call_log` checkbox defaults to false and passes these same flags.

Precedence is CLI → `BIMWRIGHT_NWD_ENABLE_CALL_LOG` → server JSON `enableCallLog` → false. Each request carries this choice to the matching plug-in, overriding its ribbon/Status **Record** toggle, saved `recordCalls`, and `BIMWRIGHT_NWD_RECORD_CALLS` without changing those settings or other gateways. The host toggle remains the fallback for legacy gateways that omit the choice; older plug-ins do not understand the override, so use matching builds. `send_code` parameters store `code_length` and `code_sha256` instead of the script body.

New call-log files rotate before an append would exceed 5 MiB. Each of the two filename groups retains at most 100 MiB for 30 days, with unique timestamped `.jsonl` archives beside the active file. Gateway and plug-in startup apply this retention even when recording is off; disabled call-record APIs still create and append nothing. Fresh oversized legacy files are archived intact and remain subject to the age and group budgets. Append, rotation and pruning share a cross-process lock; a locked or unwritable log never changes the tool outcome or falls back to unbounded writing. Maintenance only touches these generated log groups, leaving settings and ToolBaker state alone.

### Local transport limits

The plug-in accepts up to 32 TCP connections. A request envelope, including parameters, may contain up to 10 MiB of UTF-8 data; the gateway rejects larger requests before connecting, so split large item batches or shorten scripts. An idle or incomplete request frame expires after 120 seconds, and a peer that stops reading a response has a 30-second write deadline. These IO deadlines do not shorten an executing command's timeout; file operations keep their 300-second timeout. Response frames are bounded by `maxResponseBytes` while they are read, before allocating the response string.

Stopping the plug-in closes its listener and connected sockets, cancels queued work that has not started, and prevents the old lifetime from republishing or deleting a replacement target. Host work already running is allowed to finish. A timeout or lost connection can leave its result unknown; check the document before retrying a write.

### Permissions & auto mode
Every tool except `nwd_send_code` declares MCP annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`). Open-world is false except for `nwd_health_check`, whose optional update check contacts GitHub; health remains read-only and non-destructive. Claude Desktop uses them: read tools can be allowed once, and destructive tools (`nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`) always ask.

Claude Code's auto mode checks each MCP call with a classifier. To skip that check for the read-only tools, add them to `permissions.allow` in `.claude/settings.json` (replace `nwd-mcp` if your MCP entry uses another name):

```json
{
  "permissions": {
    "allow": [
      "mcp__nwd-mcp__nwd_list_recent_files",
      "mcp__nwd-mcp__nwd_list_available_targets",
      "mcp__nwd-mcp__nwd_get_current_target",
      "mcp__nwd-mcp__nwd_report_task_result",
      "mcp__nwd-mcp__nwd_get_report_history",
      "mcp__nwd-mcp__nwd_health_check",
      "mcp__nwd-mcp__nwd_get_document_info",
      "mcp__nwd-mcp__nwd_get_model_statistics",
      "mcp__nwd-mcp__nwd_get_model_tree",
      "mcp__nwd-mcp__nwd_get_item_properties",
      "mcp__nwd-mcp__nwd_batch_get_properties",
      "mcp__nwd-mcp__nwd_find_items",
      "mcp__nwd-mcp__nwd_find_items_by_name",
      "mcp__nwd-mcp__nwd_get_current_selection",
      "mcp__nwd-mcp__nwd_clear_selection",
      "mcp__nwd-mcp__nwd_select_items_by_search",
      "mcp__nwd-mcp__nwd_list_sets",
      "mcp__nwd-mcp__nwd_get_selection_set_items",
      "mcp__nwd-mcp__nwd_execute_search_set",
      "mcp__nwd-mcp__nwd_list_viewpoints",
      "mcp__nwd-mcp__nwd_get_current_viewpoint",
      "mcp__nwd-mcp__nwd_goto_viewpoint",
      "mcp__nwd-mcp__nwd_list_baked_tools",
      "mcp__nwd-mcp__nwd_list_bake_suggestions",
      "mcp__nwd-mcp__nwd_create_bake_issue_draft"
    ]
  }
}
```

Do **not** allow `mcp__nwd-mcp__*`: the wildcard also approves `nwd_send_code` and every write tool without any check. Start the agent in the project folder that holds your models, and name the exact file when you ask it to open or change one.

### ToolBaker Persistence
ToolBaker sqlite storage (`bake.db`) and usage audit logs (`audit.jsonl`) are persisted locally under:
```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

### Response size
Tool results are compact JSON. Over 64 KiB the server adds a `_response_warning` field to the result (stronger above 256 KiB); past the 1 MiB budget a result is rejected with `RESPONSE_TOO_LARGE` — except a completed write tool, which returns a compacted success summary (`response_compacted`, `mutation_applied`) instead of a false failure.
Four tools accept `output=file` to write the full result to a local same-machine file and return its path, schema and a preview: `nwd_get_model_tree` (JSON), `nwd_batch_get_properties` (SQLite), `nwd_find_items_by_name` (NDJSON) and `nwd_run_baked_tool` (auto-detected format). `nwd_send_code` has no `output` parameter; output above 1 MiB auto-spills the same way.
Spill files live under `%LOCALAPPDATA%\Bimwright\nwd-mcp\spill` and are removed on uninstall. Each file is kept 36 hours after it is written. Age is the only reason a file is deleted; there is no file-count cap. Change the time with `--spill-retention-hours <n>` (1–8760), `BIMWRIGHT_NWD_SPILL_RETENTION_HOURS` or server JSON `spillRetentionHours`; a missing or invalid value uses 36.
The guard is on by default. `--disable-response-guard` (or `BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD=0`, JSON `enableResponseGuard: false`) turns off the warnings and the 1 MiB budget. The transport cap `--max-response-bytes` (default 10 MiB) still applies, so a result above it is still rejected or spilled. `--response-warn-bytes`, `--response-strong-warn-bytes` and `--response-budget-bytes` (env `BIMWRIGHT_NWD_RESPONSE_WARN_BYTES`, `…_STRONG_WARN_BYTES`, `…_BUDGET_BYTES`; JSON `responseWarnBytes`, `responseStrongWarnBytes`, `responseBudgetBytes`) set the thresholds. They must be at least 1024 and ordered warn ≤ strong ≤ budget ≤ max, or the server does not start. Precedence for every setting is CLI → environment → JSON.

## Benchmark

Per-tool duration, result size, estimated tokens and size-policy outcome for all 35 tools on Navisworks Manage 2025: [docs/benchmarks/2026-11-16-navisworks-manage-2025.md](docs/benchmarks/2026-11-16-navisworks-manage-2025.md).

---

## Local Development & Compilation
Autodesk Navisworks API DLLs are **not redistributed** in this repository. 

- **Server and Tests:** Can be built and run on any machine without Navisworks.
  ```powershell
  dotnet test tests\Bimwright.Nwd.Tests\Bimwright.Nwd.Tests.csproj -c Debug
  ```
- **Plug-In Compilation:** Needs no Navisworks installation. Each year's project compiles against that year's Autodesk-signed API reference assemblies from the `Speckle.Navisworks.API` NuGet package, used at compile time only: they are never copied to the output or the bundle.
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug
  ```
  `scripts\package-client-setup.ps1` builds all six years (2022–2027) into the setup ZIP.

---

## The bimwright family

Open-source tools connecting AI assistants to BIM and CAD applications.

The name **bimwright** combines **BIM** with **wright**, an old word for a maker or builder—as in *shipwright*.

See [how the gateway names are chosen](https://github.com/bimwright/.github/blob/master/profile/README.md#naming).

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) — Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) — Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) — Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) — Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) — Vietnamese-first BIM knowledge base

---

## License

Apache-2.0. See [LICENSE](LICENSE) for details.

Navisworks and Autodesk are registered trademarks of Autodesk, Inc. bimwright is an independent open-source project and is not affiliated with, sponsored by, or endorsed by Autodesk, Inc.
