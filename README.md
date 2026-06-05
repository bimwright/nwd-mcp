<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#capabilities--architecture"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#tool-surface"><img src="https://img.shields.io/badge/MCP-30%20tools-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  English · <a href="README.vi.md">Tiếng Việt</a>
</p>

`nwd-mcp` is a professional-grade Model Context Protocol (MCP) gateway for **Autodesk Navisworks Manage** automation. It enables AI agents to query, inspect, navigate, and script Autodesk Navisworks Manage desktop sessions locally over stdin/stdout.

---

## Capabilities & Architecture
- **Supported Host:** Autodesk Navisworks Manage only. (Freedom and Simulate are not supported).
- **Supported Versions:** 2022, 2023, 2024, 2025, 2026, and 2027.
- **Two-Process Model:** Lightweight `.NET 8` console server communicating with a version-specific `.NET Framework 4.8` (`net48`) in-process plug-in over localhost TCP NDJSON.
- **Security First:** Per-session random cryptographic token validation, loopback-only TCP binding, and absolute file path sanitization in model responses.
- **Multi-Instance Routing:** Automatically detects multiple running Navisworks Manage instances and supports switching targets dynamically.

---

## Current Status

| Component | Status |
|---|---|
| MCP gateway server (.NET 8) | ✅ Builds warning-clean (Debug + Release) |
| Unit tests (40 xUnit) | ✅ All passing |
| Plug-in handler implementations | ✅ Verified against a live Navisworks Manage session |
| Plug-in projects (net48) | ✅ Compile against the Navisworks Manage SDK |

> **Note:** The plug-in handler layer uses real Navisworks .NET API calls (not stubs or fabricated
> data) and has been exercised against a live Navisworks Manage instance. See [walkthrough.md](walkthrough.md)
> for the first-run checklist.

---

## Tool Surface

Phase 1 provides exactly **30 tools** when all toolsets are enabled. Every tool uses the `nwd_*` prefix.

### 1. Target/Meta Tools (3)
* `nwd_list_available_targets` — List all active discovered Navisworks sessions.
* `nwd_get_current_target` — Report which session the server is currently pointed at.
* `nwd_switch_target` — Point the gateway at a different active session.

### 2. Query/Read Tools (8)
* `nwd_health_check` — Check active session status and heartbeat.
* `nwd_get_document_info` — Retrieve active document name, file path, and model count.
* `nwd_get_model_statistics` — Retrieve counts of elements, models, and selections.
* `nwd_get_model_tree` — Retrieve a bounded model tree node hierarchy.
* `nwd_get_item_properties` — Retrieve property categories and property lists for an element.
* `nwd_batch_get_properties` — Retrieve properties for multiple elements at once.
* `nwd_find_items` — Query elements using advanced property/category filters.
* `nwd_find_items_by_name` — Search elements by display name.

### 3. Selection Tools (3)
* `nwd_get_current_selection` — Retrieve element IDs of the active user selection.
* `nwd_clear_selection` *(Write)* — Clear the active selection.
* `nwd_select_items_by_search` *(Write)* — Select items matching property/name filters.

### 4. Selection Sets Tools (3)
* `nwd_list_sets` — Retrieve selection and search sets, recursing folders.
* `nwd_get_selection_set_items` — Retrieve elements inside a set.
* `nwd_execute_search_set` *(Mixed)* — Execute a search set; optionally select matches.

### 5. Viewpoint/Navigation Tools (4)
* `nwd_list_viewpoints` — Enumerate saved viewpoints and folders.
* `nwd_get_current_viewpoint` — Retrieve current camera and view state.
* `nwd_goto_viewpoint` *(Write)* — Navigate the viewport camera to a saved viewpoint.
* `nwd_save_viewpoint` *(Write)* — Save the active view as a named viewpoint.

### 6. Visibility Tools (2)
* `nwd_hide_items` *(Write)* — Hide/show specified elements.
* `nwd_unhide_all` *(Write)* — Reset all hidden elements to visible.

### 7. Escape Hatch Scripting (1)
* `nwd_send_code` *(Write, Opt-in)* — Compile and execute in-process C# code against the Navisworks API.

### 8. ToolBaker Governed Tools (6)
* `nwd_list_baked_tools` — List all verified compiled reusable tools.
* `nwd_run_baked_tool` *(Write)* — Run an accepted baked tool by name with parameters.
* `nwd_list_bake_suggestions` — List adaptive workflow suggestions.
* `nwd_accept_bake_suggestion` *(Write)* — Validate, compile, and deploy a suggested tool.
* `nwd_dismiss_bake_suggestion` *(Write)* — Dismiss/snooze an active suggestion.
* `nwd_create_bake_issue_draft` — Generate a draft GitHub issue for requested tools.

---

## Safety Configurations

### Read-Only Mode
Strict read-only mode can be enforced via the `--read-only` flag or the `BIMWRIGHT_NWD_READ_ONLY=1` environment variable.
- All write-capable toolsets are omitted from registration.
- Mixed tools (e.g. `nwd_execute_search_set`) are modified to force read-only parameter bounds (`select=false`) and output a `read_only_enforced` response marker.
- The total read-only tool surface is exactly **20 tools**.

### SendCode Two-Sided Opt-in
Dynamic C# scripting (`nwd_send_code`) is **disabled by default**. It is only exposed when:
1. The server is booted with `--enable-send-code` or `BIMWRIGHT_NWD_ENABLE_SEND_CODE=1`.
2. The plug-in detects `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1` in its environment.

### ToolBaker Persistence
ToolBaker sqlite storage (`bake.db`) and usage audit logs (`audit.jsonl`) are persisted locally under:
```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

---

## Local Development & Compilation
Autodesk Navisworks API DLLs are **not redistributed** in this repository. 

- **Server and Tests:** Can be built and run on any machine without Navisworks.
  ```powershell
  dotnet test tests\Bimwright.Nwd.Tests\Bimwright.Nwd.Tests.csproj -c Debug
  ```
- **Plug-In Compilation:** Requires a local Navisworks Manage installation.
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Manage 2026"
  ```
  If Navisworks Manage is installed in a non-default path, override the hint property:
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="D:\Autodesk\Navisworks 2026"
  ```

---

## The bimwright family

Hand-forged MCP gateways for the AEC toolchain — one architecture, predictable / auditable / reversible:

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) — Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) — Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) — Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) — Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) — Vietnamese-first BIM knowledge base

---

## License

Apache-2.0. See [LICENSE](LICENSE) for details.

Navisworks and Autodesk are registered trademarks of Autodesk, Inc. bimwright is an independent open-source project and is not affiliated with, sponsored by, or endorsed by Autodesk, Inc.
