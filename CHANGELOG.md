# Changelog

## Unreleased

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
