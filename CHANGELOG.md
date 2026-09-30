# Changelog

## Unreleased

### Changed
- `nwd_send_code` is on by default. `--disable-send-code` or `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0`, and `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0`, turn it off. `--read-only` still removes it.
- Destructive tools now say what they change and how to undo it: `nwd_open_file` (discarding unsaved changes cannot be undone), `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`.
- The server instructions end with a **Safety & permissions** paragraph: confirm exact files or items before discarding or changing user files, and do not retry a denied call through a baked tool or `nwd_send_code`.
- The server installs at the fixed path `%LOCALAPPDATA%\Bimwright\nwd-mcp\server\current\nwd-mcp.exe` instead of a versioned folder, matching the other bimwright gateways. Reinstalls swap the whole `current` folder so no stale files remain; earlier `server\<version>` copies are kept and reported — repoint MCP client entries to the new path.

### Added
- MCP tool annotations on every tool except `nwd_send_code`: `readOnlyHint`, `destructiveHint`, `idempotentHint`, and `openWorldHint=false`. Claude Desktop can allow the 23 read tools once; `nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, and `nwd_dismiss_bake_suggestion` always ask. **Migration:** no parameters or defaults changed. Clients that honor annotations may now ask before those five tools.
- README **Permissions & auto mode** section with a Claude Code `permissions.allow` list of the read-only tools, and a warning against `mcp__nwd-mcp__*`.
- **Record** switch for every tool call, off by default. The ribbon, Status window, `recordCalls` in `nwdmcp.config.json`, and `BIMWRIGHT_NWD_RECORD_CALLS` share it. While on, calls append to `%LOCALAPPDATA%\Bimwright\nwd-mcp\mcp-calls.jsonl`. `send_code` stores length and SHA-256 instead of the script.
- `nwd_list_recent_files`, `nwd_open_file`, and `nwd_import_model`. A default launch registers every toolset (33 tools). Open refuses unsaved changes unless `discard_changes` is true. Import `append` adds a model; `merge` combines it. Open and import wait up to 5 minutes.

### Fixed
- Item ids below a model root were empty, so `nwd_get_model_tree`, `nwd_find_items`, `nwd_find_items_by_name`, and `nwd_get_current_selection` returned no usable ids and `nwd_hide_items` / `nwd_get_item_properties` could not target them. Ids now look like `0:0:6:3`, and each parent's children are numbered once per call: on a 10,717-item model, `nwd_get_model_tree` depth 6 / 5,000 nodes takes ~0.1 s and `nwd_find_items_by_name` 500 hits ~0.3 s.
- `nwd_get_model_statistics` returns `item_count`, as its description says.
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
