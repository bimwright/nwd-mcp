# Changelog

## v2.5.1 - 2026-11-23

Version numbers now follow the shared release number of the four bimwright gateways (rvt-mcp, dwg-mcp, nwd-mcp, ipt-mcp). The jump from v1.1.0 to v2.5.1 does not mark breaking changes; the entries below list what changed since v1.1.0.

### Added
- The toast title names the agent of the latest call from its MCP client name: `nwd-mcp 2025 · Claude` (Claude, Codex, Cline, Cursor, Windsurf, Antigravity, Gemini, Copilot, VS Code; any other client shows its own name, cleaned and capped at 20 characters). The gateway sends the name with every command as `client_name`; it is display text only. Update the gateway and plug-in together; an older gateway leaves the name out.
- The Navisworks process ID (`· PID 12345`) is laid out in the title and fades in when you point at the card, with or without branding.
- **Pause for today**: a button at the foot of the expanded card and a switch in Status hide every card, errors included, in every Navisworks on this computer until local midnight. On the card it turns into **Resume**, so a mis-click is undone at once. **Toasts** on the ribbon and turning toasts on end the pause. The choice is saved as the local date in `toastSnoozedDate`; while paused, `nwd_report_task_result` returns `toast_shown: false`.
- `BIMWRIGHT_KEEP_ALL_LOGS=1` (also `true`, `yes`, `on`) keeps every call log and saved report, for development machines. Set it for the gateway and for Navisworks: call logs still rotate to new timestamped archives, but nothing is pruned by age, by the 100 MiB group budget or by the 500-report cap. It does not turn recording on or change what is recorded. Spill files, ToolBaker data and target descriptors keep their own cleanup. The gateway writes one line to stderr at startup when it is on. Unset, user machines keep the current limits.

### Fixed
- `install.ps1` no longer fails when an MCP client still runs the installed `nwd-mcp.exe`. The new server is moved into `server\current` as before; the previous copy is kept whole in `server\current.backup-<id>` while its exe is in use, reported as `In use  : <path> - restart MCP clients; removed at next install`, and the script exits 0. Before, it exited 1 after the swap and deleted the other files (such as `e_sqlite3.dll`) from under the running server. Each install removes earlier `current.backup-*` folders that are no longer in use. A failed swap now restores the previous server under PowerShell 7 too, where a locked file could leave `current` half-moved.
- `uninstall.ps1 -WhatIf` previewed nothing and really uninstalled: it removed the add-in bundle, `server\` and `spill\`. `-WhatIf` and `-Confirm` now reach `install.ps1 -Uninstall`, and a failed uninstall exits non-zero.
- `nwd_send_code` errors now match rvt-mcp: a compile error lists the first 20 diagnostics and a runtime error gives the cause's type and message (behind aggregate, invocation and type-initializer wrappers), both with Windows paths and secrets masked. Before, a runtime error returned the full exception text with its stack trace, and compile errors were neither capped nor masked.
- Call logs (`nwd-mcp-calls.jsonl` and `mcp-calls.jsonl`) mask secrets and Windows paths in `error` and `result`, and keep `nwd_send_code` `data` only as its byte count and SHA-256, as rvt-mcp does. Before, results, errors and script data were stored as returned.
- An auto-spilled `nwd_send_code` result reports `mutation_applied: null` (rvt-mcp parity): the script may or may not have changed the model.
- The `nwd_send_code` description lists the default imports, shows how to batch edits with `doc.BeginTransaction(...)` and `Commit()`, says that Navisworks cannot roll a transaction back, and says how errors come back.

## v1.1.0 - 2026-11-16

First release since v1.0.0. It also ships the v1.0.1 changes below, which were never published on their own.

### Fixed
- After `nwd_import_model` appended or merged into a saved file, every later write was refused with `DOCUMENT_CHANGED`: Navisworks clears the file name ("Untitled"), which the plug-in reads as a new document. A successful import into the confirmed document now moves the confirmation to it, as `nwd_open_file` does. Found by the v1.1.0 release benchmark.
- `nwd_send_code` no longer fails every compile with CS0009 when a loaded DLL's metadata cannot be read (rvt-mcp #38): such references are left out and listed in `skipped_references`.
- Call logs are bounded: files rotate before an append would exceed 5 MiB, and each filename group keeps at most 100 MiB for 30 days. Gateway and plug-in startup apply retention even when recording is off; append, rotation and pruning share a cross-process lock and never fall back to unbounded writing.
- Local transport is bounded: up to 32 TCP connections, 10 MiB request envelopes (rejected by the gateway before connecting), 120-second idle/incomplete frames and a 30-second response write deadline; file operations keep their 300-second timeout. Stopping the plug-in closes its listener and connected sockets, cancels queued work that has not started, and cannot republish or delete a replacement target. See [local transport limits](README.md#local-transport-limits).
- Empty-document camera reads guard `HasFocalDistance` and return `focal_distance: null` instead of throwing.
- Saved viewpoint/set names containing literal `/` now round-trip. Lists retain `name` and add `id` and `path`; consumers accept these identifiers. Escaped paths start with `path:/`, encode `~` as `~0` and `/` within a segment as `~1`. GUID-less SDK items use the escaped path as their ID. Legacy unambiguous names/paths still work; ambiguous names fail rather than silently selecting the first match.
- Active ToolBaker lists exclude accepted/archived/permanently dismissed suggestions and current snoozes. Dismiss persists a UTC deadline exactly 30 days ahead, reports it, and repeated snooze calls do not extend it. Expired snoozes become effectively open without mutating data during a read-only listing. Legacy dismissed rows without a deadline stay hidden.

### Added
- Tool-call logging is off by default and controlled by `--enable-call-log` / `--disable-call-log` (CLI overrides `BIMWRIGHT_NWD_ENABLE_CALL_LOG` and server JSON `enableCallLog`). The gateway records every MCP call, including local tools and failures, in `nwd-mcp-calls.jsonl`; each forwarded request overrides the matching plug-in's `mcp-calls.jsonl` recording without changing saved host settings. Legacy gateways keep the host toggle fallback. `send_code` parameters retain only script length and SHA-256. The MCPB source profile adds a false-default `call_log` setting; no new bundle is published by this change.
- `nwd_health_check` now reports loaded server/plugin runtime identities, versions, build ids and version/revision alignment. Host failure preserves the running server identity; legacy plugins without identity report unknown evidence. Disk hashes are explicitly scoped to identity capture, not loaded memory or publisher verification.
- Optional `check_updates` (offline by default; recommended once at session start) compares both components with one official stable release and tag revision using a shared 5-second budget and bounded responses. Missing evidence/failures remain unknown. Notifications distinguish new releases, revision review, unverified identity, external lookup failure and informational newer local versions. `protected_components`/reasons and separate `unobserved_components` prevent unsafe full-bundle proposals. Only `update_guidance.mode=prepare_update` with `bundle_update_proposal_allowed=true` returns installation steps and requests authorization; other modes only check/report/review. Health never downloads packages, installs, saves/closes documents or restarts anything.
- Full informational version in `initialize.serverInfo`, per-process health tool runtime metadata in `tools/list`, and `client_verification` evidence for reconnect/cache checks. The server cannot itself verify a client catalog refresh. Health remains available in read-only mode; its open-world annotation is true for the optional GitHub lookup. Tool counts and existing no-argument calls are unchanged.
- Version/revision assembly attributes enabled for all six plugin shells, plus runtime, release comparison and stdio protocol regression tests.
- Response guard settings (family release checklist, rvt-mcp names). `--disable-response-guard` / `--enable-response-guard` (default on; `BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD`, JSON `enableResponseGuard`) and `--response-warn-bytes`, `--response-strong-warn-bytes`, `--response-budget-bytes` (env `BIMWRIGHT_NWD_RESPONSE_*_BYTES`, JSON `responseWarnBytes`, `responseStrongWarnBytes`, `responseBudgetBytes`; defaults 64 KiB / 256 KiB / 1 MiB). With the guard off there are no warnings and the budget becomes the transport cap `--max-response-bytes`, which stays active.
- `--spill-retention-hours <n>` (1–8760, `BIMWRIGHT_NWD_SPILL_RETENTION_HOURS`, JSON `spillRetentionHours`); a missing or invalid value uses 36.
- The client setup package script writes `<zip>.sha256` (`<hash>  <file name>`) next to the ZIP for the GitHub Release.
- `nwd_send_code` takes `data` (one JSON value; the script reads it as `data["key"]`, a Newtonsoft JToken) and `result_contract` (`legacy` default, or `task_v1`: the script returns `McpTaskResult.Pass(value)` or `McpTaskResult.Fail(code, message, details?)`, the response carries `_task{contract, status, error}`, a failed or missing result sets `ok=false` with `TASK_RESULT_REQUIRED`). rvt-mcp / ipt-mcp parity. Invalid `data` or contract is refused before anything is sent. Only scripts that receive `data` get the variable, and compile errors keep the caller's line numbers.
- `nwd_send_code` returns objects as JSON (ipt-mcp parity): anonymous objects, POCOs, dictionaries and lists in `result` are JSON objects and arrays instead of their `ToString()` text; enums stay names. A return value that contains a Navisworks API object sets `ok=false` with a hint to return a DTO, because such objects reach the whole model tree.
- `nwd_report_task_result` and `nwd_get_report_history` (family contract FC-RPT-01..05, `meta` toolset, read-only). A report shows a card on the Navisworks toast and is saved under `%LOCALAPPDATA%\Bimwright\nwd-mcp\reports` (newest 500 kept) with the write tools run since the previous report, also when Navisworks is not reachable (`toast_shown=false`). Responses of document writes carry `_report` until the agent reports; scripts and baked tools get one conditional reminder. The plug-in answers reports off the UI thread, so a report arrives while Navisworks is busy. Default surface 35 tools, read-only 25.
- Document confirmation before writes (family contract FC-DOC-01..06, FC-PAIR-01..03, capability `document_target_v1`). Writes run only on the document confirmed with the new `nwd_switch_target(..., verify=true)`, which reads the open document with one health check. Before: a write went to whatever file was open, even one the user had opened after the agent looked.
  - Until a document is confirmed, writes return `TARGET_UNCONFIRMED` and are not sent. A new binding starts unconfirmed; selecting the same instance again keeps the confirmation.
  - The plug-in checks again on the Navisworks UI thread right before the handler: another file open now returns `DOCUMENT_CHANGED` and the command does not start.
  - `nwd_open_file` moves the confirmation to the file it opens, if the confirmed file was still open when it started.
  - A server and plug-in from different releases return `PAIRING_REQUIRED` for writes, in both directions. Reads work. `nwd_goto_viewpoint`, `nwd_clear_selection`, `nwd_select_items_by_search` and installing a baked tool (`nwd_accept_bake_suggestion`) need no confirmation.
  - `nwd_get_current_target` and `nwd_switch_target` report `document_confirmed`, `confirmed_document_id` and `confirmed_document_title`. Plug-in responses carry `document_id` / `document_title`, and descriptors list `capabilities`.

### Changed
- Plug-ins for every Navisworks year build without Navisworks installed: each shell compiles against that year's Autodesk-signed API reference assemblies from the `Speckle.Navisworks.API` NuGet package, compile-time only, so they are never copied to the output or the bundle. The setup ZIP now carries all six years (2022–2027), and CI builds each year. The 2022–2024 and 2026–2027 shells also gained the `IsExternalInit` polyfill and the correct `Autodesk.Navisworks.Controls` reference, which they lacked since the 2025 fix. Only Navisworks Manage 2025 has been run live; the other years are build-verified against their real API but not yet tested in Navisworks.
- `nwd_execute_search_set(select=true)` selects the matches in read-only mode too: selection is transient UI state, like `nwd_select_items_by_search` and `nwd_clear_selection` (family contract D11). The gateway no longer forces `select=false` or sends `read_only_enforced`; the plug-in still honours that flag from an older gateway.
- Busy Navisworks returns `HOST_BUSY` (family contract FC-BUSY-01..05, ipt-mcp parity). A modal dialog waiting for the user refuses commands before they are queued, and again on the UI thread, because a dialog's message loop runs queued work. A command the UI thread did not start by its start deadline (timeout minus 2 s) is abandoned and never runs later. Both say the command was not started. `TIMEOUT` now only means a started command with an unknown outcome; the plug-in answers it 1 s before the gateway gives up. `nwd_health_check` answers from the transport thread while Navisworks is busy (`host_busy`, `busy_reason`, `answered_without_ui_thread`), and `nwd_switch_target(verify=true)` then returns `HOST_BUSY` instead of confirming nothing.
- `--read-only` filters tools by their read-only annotation instead of dropping whole toolsets: 23 tools instead of 21. `nwd_goto_viewpoint`, `nwd_clear_selection` and `nwd_select_items_by_search` are now available (they change the view or selection, not the model), and `nwd_switch_target` is removed (family contract D10/D11); pin the target with `--target pid:<n>`.
- Target binding (family contract FC-TGT-01..06). The gateway binds to one Navisworks instance identified by PID and process start time, and never falls through to another one. Before: with no choice it took the newest heartbeat, and a bound instance that exited was silently replaced by another live one, so writes could reach the wrong Navisworks.
  - With two or more matching instances and no choice, commands return `AMBIGUOUS_TARGET` with the candidates instead of picking one.
  - When the bound instance exits: `TARGET_LOST` (explicit choice), `TARGET_CHANGED` (one other instance, proposed but not selected), `AMBIGUOUS_TARGET` or `NO_TARGET`. The command is not sent, and the binding stays lost until `nwd_switch_target` confirms a target. A reused PID is detected by its start time.
  - `nwd_switch_target` accepts `pid:<n>`, `id:<target_id>`, a bare `navis-…` id, a year 2022–2027 or `auto` (also for `--target` / `BIMWRIGHT_NWD_TARGET`, where an invalid selector stops the server). R-codes and malformed selectors return `INVALID_TARGET_SELECTOR`. A year or `auto` never moves an existing binding (`TARGET_REBIND_REQUIRED`); leaving a live, explicitly selected instance needs the new `rebind=true`. Refusals return `{ok:false, error:{code, message, target}}` instead of `{ok:false}`.
  - `nwd_get_current_target` no longer selects a target; it reports `binding`, `binding_state`, `generation`, `bound_pid`, `window_title`, `next_target_id` and `confirmation_required` next to the old fields. `nwd_list_available_targets` adds `target_id`, `host_year`, `pid`, `process_start_utc`, `identity_verified`, `window_title`, `is_current` and `last_heartbeat_utc`.
  - The first selection becomes the binding only once its first request is written; a failed first connect returns `TARGET_UNAVAILABLE` and keeps the state `none`, so an instance opened later is still a first selection, not `TARGET_LOST`.
  - Every tool call pins the binding generation on arrival; a call that was pending across a switch is not sent (`TARGET_CHANGED`). A call that arrived before any binding may go only to the first binding; if the binding moved since, it is not sent either. A connection that closes after the command was written returns `TARGET_INTERRUPTED` (`sent: true`, `outcome: "unknown"`) instead of `TARGET_UNAVAILABLE`.
  - Failed tool results carry the target payload as `error.target` (`sent`, `outcome`, `candidates`, `recommended_next_tool`, `next_step`).
- Discovery descriptors are schema v3 (`schema_version`, `host_app`, `host_year`, `pid`, `process_start_utc`, `transport`) and keep every old field, so older gateways still read them. Liveness no longer uses the heartbeat: a descriptor is live while its process runs with the recorded start time, and the gateway deletes descriptors it has proven dead. Instances sort by year, then newest process start.
- Spill files are kept 36 hours instead of 24, and the 50-file cap is gone: age is the only reason a spill file is deleted, so the file an agent was pointed to is never removed early. With a small budget the spill receipt shrinks (preview, schema, then summary text) but always keeps the file path; before, a receipt over the budget was replaced by a generic success message without the path.
- The server refuses to start when response limits are below 1024 bytes or out of order (warn ≤ strong ≤ budget ≤ `maxResponseBytes`). A `--max-response-bytes` below the 1 MiB default budget now also needs a matching `--response-budget-bytes`.

## v1.0.1 - 2026-10-02 (not published; shipped in v1.1.0)

### Added
- Toast position controls in Status: independent horizontal/vertical alignment for four corners, opt-in title-row dragging, remembered owner-relative DIP offsets and Reset position. Changes apply/save immediately; changing corners clears offsets, disabling drag preserves them. Monitor-work-area clamping, anchored expansion with opposite-side fallback, directional enter/exit and smooth corner changes respect reduced motion. No cross-app collision coordination.
- Latest-tool and activity-row tooltips include sanitized outcome, success/failure, captured completion time and measured duration when available.
- Deliberate-hover activity preview: all outcomes in the current toast card, with tool name, compact result, success/failure and local 24-hour completion time (`HH:mm:ss`). Starts at the newest three compact, individually bordered cards; each shows a title/time line and one ellipsized result line, with the full bounded result in its tooltip. Scroll up through older calls using the thin RVT-style scrollbar. No scroll/refresh instruction footer. Virtualized rows and immutable indexed snapshots keep a growing card compact. Expands with height/fade/slide easing after a 200 ms intent delay and collapses after a short leave grace period. Incoming results append immediately while hovered: follow the latest at the bottom, preserve the scroll position while reading older calls, and resume following when returning to the bottom. A new hover after full collapse starts at the latest; interrupted collapse preserves reading position. One/two-call cards grow without clipping new rows. Independent of branding, reduced-motion aware, no focus stealing or additional call-log persistence. Summaries have bounded text lengths and redact credentials/full paths; raw requests, result JSON and script source are not retained.

### Fixed
- Script failures inside a successful transport envelope now count as Failed instead of Success in the activity card.
- Hide/show, selection and search-set results describe the actual outcome instead of a generic completion. Empty script stdout no longer hides a real result; object/array script results use a bounded summary rather than their raw contents.

## v1.0.0 - 2026-09-30

Client setup ZIP: `NwdMcp.Setup-v1.0.0-win-x64.zip` (self-contained `nwd-mcp.exe`). **Plugin year in this ZIP:** Navisworks Manage **2025**. Source still supports 2022–2027. NuGet `Bimwright.Nwd.Server` 1.0.0 is server-only (no plug-in).

### Changed
- Destructive tools now say what they change and how to undo it: `nwd_open_file` (discarding unsaved changes cannot be undone), `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`.
- The server instructions end with a **Safety & permissions** paragraph: confirm exact files or items before discarding or changing user files, and do not retry a denied call through a baked tool or `nwd_send_code`.
- The server installs at the fixed path `%LOCALAPPDATA%\Bimwright\nwd-mcp\server\current\nwd-mcp.exe` instead of a versioned folder, matching the other bimwright gateways. Reinstalls swap the whole `current` folder so no stale files remain; earlier `server\<version>` copies are kept and reported — repoint MCP client entries to the new path.

### Added
- Response-size policy on every tool result (rvt-mcp parity, enforced server-side). Over 64 KiB adds a `_response_warning` field to the result JSON (stronger above 256 KiB); past the 1 MiB budget a read tool is rejected with `RESPONSE_TOO_LARGE`, while a completed write returns a compacted success (`ok`, `response_compacted`, `mutation_applied`) instead of a false failure. `nwd_send_code` and `nwd_run_baked_tool` report `mutation_applied: null` when the outcome is indeterminate.
- `output=file` on `nwd_get_model_tree` (JSON), `nwd_batch_get_properties` (SQLite), `nwd_find_items_by_name` (NDJSON) and `nwd_run_baked_tool` (auto-detected format) writes the full result to `%LOCALAPPDATA%\Bimwright\nwd-mcp\spill` and returns its path, schema and a preview. `nwd_send_code` output above 1 MiB auto-spills the same way; there is no `output` parameter on it. Spill files are kept 24 hours, capped at 50, and removed on uninstall. A spill that cannot be written reports `SPILL_FAILED` with `operation_completed` rather than pretending the call failed.
- MCP tool annotations on every tool except `nwd_send_code` (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint=false`) for Claude Code's permission auto mode, and Claude Desktop: the 23 read tools can be allowed once, while `nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, and `nwd_dismiss_bake_suggestion` always ask. README **Permissions & auto mode** has the `permissions.allow` list of the read-only tools and a warning against `mcp__nwd-mcp__*`. **Migration:** no parameters or defaults changed.
- **Record** switch for every tool call, off by default. The ribbon, Status window, `recordCalls` in `nwdmcp.config.json`, and `BIMWRIGHT_NWD_RECORD_CALLS` share it. While on, calls append to `%LOCALAPPDATA%\Bimwright\nwd-mcp\mcp-calls.jsonl`. `send_code` stores length and SHA-256 instead of the script.
- `nwd_list_recent_files`, `nwd_open_file`, and `nwd_import_model`. A default launch registers every toolset (33 tools). Open refuses unsaved changes unless `discard_changes` is true. Import `append` adds a model; `merge` combines it. Open and import wait up to 5 minutes.
- Live benchmark of all 33 tools on Navisworks Manage 2025 (duration, result size, estimated tokens, size policy, spill-to-file): `docs/benchmarks/2026-09-30-navisworks-manage-2025.md`.

### Fixed
- Item ids below a model root were empty, so `nwd_get_model_tree`, `nwd_find_items`, `nwd_find_items_by_name`, and `nwd_get_current_selection` returned no usable ids and `nwd_hide_items` / `nwd_get_item_properties` could not target them. Ids now look like `0:0:6:3`, and each parent's children are numbered once per call: on a 10,717-item model, `nwd_get_model_tree` depth 6 / 5,000 nodes takes ~0.1 s and `nwd_find_items_by_name` 500 hits ~0.3 s.
- `nwd_get_model_statistics` returns `item_count`, as its description says.
- `nwd_send_code` failed on Navisworks 2025 with `TypeInitializationException` (`PerTypeValues`1`): Navisworks has no binding redirects for the Roslyn dependencies we ship. The plug-in now resolves those assemblies from its own folder. Scripts also ran on a worker thread, where the Navisworks API is not valid; they now compile off the UI thread and run on it, return the last expression as `result`, and report the full exception chain.
- A command that waited in the UI queue past the caller's timeout still ran, and long walks kept Navisworks busy after the caller gave up. Stale commands are now skipped with `TIMEOUT`, and `get_model_tree`, `find_items`, `find_items_by_name`, `batch_get_properties`, and `get_model_statistics` stop at the deadline and mark the result (`truncated: "deadline"`, `item_count_complete: false`).
- The target descriptor always reported no document: the heartbeat read the document off the UI thread. The title and path are now read on the UI thread after each command and on idle.
- When the 10 MB plug-in wire cap rejects a write command's reply, the error now says the command already ran and not to blindly retry it; the generic narrowing hint no longer names specific parameters.
- Failed tool calls (`{"ok":false,...}`) now also set the MCP `isError` flag.
- The server enforces `maxResponseBytes` on plug-in replies, and tool results are compact JSON (about half the size of the previous indented output).
- `contains`, `startsWith`, and `endsWith` filters in `nwd_find_items` and `nwd_select_items_by_search` matched nothing. They now match, case-insensitive.
- A command that reached the plug-in before a UI `SynchronizationContext` was captured ran on a worker thread and could crash Navisworks (0x80131509). It now falls back to the UI `Dispatcher` (PR #1 by @huangting2015).

## v0.1.2 - First GitHub Release

Client setup ZIP: `NwdMcp.Setup-v0.1.2-win-x64.zip` (self-contained `nwd-mcp.exe`). **Plugin year in this ZIP:** Navisworks Manage **2025** (the only Manage install on the pack machine). Source still supports 2022–2027.

### Fixed

- Navisworks 2025 API compatibility for model-tree walk and the host gate.
- Tool dispatch hardened before the public push.

### Changed

- Plugin handlers marked verified against a live Navisworks Manage session (supersedes the v0.1.1 “not live-tested” status line).
- README Install section, locale links, tool counts, and path-sanitization scope.
- Japanese and Simplified Chinese README mirrors.

## v0.1.1 - Handler Layer Completion

### Added
- **Unified ModelItem ID encoding** (`ModelItemHelper.cs`) using deterministic index-path scheme (`modelIndex:childIndex:...`) shared by every handler that produces or consumes item IDs.
- **10 plug-in handlers fully implemented** with real Navisworks .NET API calls:
  `hide_items`, `unhide_all`, `select_items_by_search`, `save_viewpoint`, `goto_viewpoint`,
  `list_viewpoints`, `get_current_viewpoint`, `list_sets`, `get_selection_set_items`, `execute_search_set`.
- **6 existing read handlers updated** to produce/consume consistent ModelItem IDs:
  `find_items`, `find_items_by_name`, `get_model_tree`, `get_item_properties`, `batch_get_properties`, `get_current_selection`.
- `walkthrough.md` documenting what is verified and what awaits live Navisworks testing.

### Changed
- `README.md` — Added "Current Status" section with honest plug-in verification status.

### Fixed
- Resolved all 6 nullable-reference warnings (`ToolCompiler.cs`, `AcceptBakeSuggestionHandler.cs`); the server now builds warning-clean in Debug and Release.
- Extracted the duplicated Navisworks search-filter logic into a shared `SearchConditionBuilder` (used by `find_items` and `select_items_by_search`).

### Removed
- `UnitTest1.cs` — Empty test template file.

### Status
- Server: 0 warnings, 0 errors (Debug + Release).
- Tests: 40 xUnit tests, all passing.
- **Plug-in handlers are written but not compiled/tested against a live Navisworks instance.**

---

## v0.1.0 - Initial Scope Release

Initial release of the Bimwright Navisworks MCP repository (`nwd-mcp`).

### Added
- **MCP gateway server** (.NET 8, stdio, `bimwright-nwd`) targeting Autodesk Navisworks Manage 2022-2027.
- **In-process desktop plug-ins** (net48, versions 2022-2027) with Localhost TCP NDJSON transport, cryptographic token authentication, and UI-thread invocation.
- **21 Navisworks Domain/Meta Commands** in Phase 1:
  - `health_check`
  - `get_document_info`
  - `get_model_statistics`
  - `get_model_tree`
  - `get_item_properties`
  - `batch_get_properties`
  - `find_items`
  - `find_items_by_name`
  - `get_current_selection`
  - `clear_selection`
  - `select_items_by_search`
  - `list_sets`
  - `get_selection_set_items`
  - `execute_search_set`
  - `list_viewpoints`
  - `get_current_viewpoint`
  - `goto_viewpoint`
  - `save_viewpoint`
  - `hide_items`
  - `unhide_all`
  - `send_code` (Roslyn dynamic C# scripting)
- **Bimwright Platform Features**:
  - ToolBaker self-evolution engine (`nwd_list_baked_tools`, `nwd_run_baked_tool`, `nwd_list_bake_suggestions`, `nwd_accept_bake_suggestion`, `nwd_dismiss_bake_suggestion`, `nwd_create_bake_issue_draft`).
  - Strict read-only mode (`--read-only` or `BIMWRIGHT_NWD_READ_ONLY=1`) dropping all write toolsets and forcing safe parameter bounds (e.g. `nwd_execute_search_set select=false`).
  - Cryptographic 32-byte authentication tokens generated per session.
  - Core test suite (40 xUnit tests) verifying configuration overrides, toolset filtering, transport contracts, and registration snapshots against schema golden files.
