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
- **Multi-Instance Routing:** Automatically detects multiple running Navisworks Manage instances and supports switching targets dynamically.

Navisworks shows one activity card for MCP commands. Toasts are on by default and stay up for 20 seconds after the latest result (10, 20, 30, or 60). Hover pauses the card; leaving it starts the selected interval again. The BIMwright wordmark stays off unless Show branding is turned on. `nwd_health_check` does not appear on the card. The **Bimwright** ribbon tab has **Toasts**, **Toast Brand**, **Record**, and **Status**. **Record** is off until you turn it on; it then appends every tool call to `%LOCALAPPDATA%\Bimwright\nwd-mcp\mcp-calls.jsonl`. `send_code` is stored as a length and SHA-256, not the script. Settings live in `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json`. `BIMWRIGHT_NWD_ENABLE_TOAST` and `BIMWRIGHT_NWD_RECORD_CALLS` override those switches at the next launch.

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
> for the first-run checklist.

---

## Install

Download the client setup ZIP from [GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest). It includes a self-contained MCP server and the Navisworks Manage plugin years compiled for that release (see `manifest.json`). The v0.1.2 ZIP ships **2025** only. Other years: build from source against that year’s Manage SDK.

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

Do **not** `dotnet tool install -g Bimwright.Nwd.Server` — that is not the supported client install.

**Developer:** `dotnet build` a `plugin-navisNN` project, then `pwsh scripts\install-bundle.ps1 -Year 2025 -Configuration Release`.

Useful flags after wiring: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`. `nwd_send_code` is on unless you pass `--disable-send-code` (or `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0`) and set `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` on the Navisworks process (see [Safety](#safety-configurations)).

---

## Tool Surface

A default launch registers every toolset: **33 tools**, including `nwd_send_code`. `--disable-send-code` leaves **32**. `--read-only` leaves **21**. Every tool uses the `nwd_*` prefix.

### 1. Target/Meta Tools (3)
* `nwd_list_available_targets` — List all active discovered Navisworks sessions.
* `nwd_get_current_target` — Report which session the server is currently pointed at.
* `nwd_switch_target` — Point the gateway at a different active session.

### 2. File Tools (3)
* `nwd_list_recent_files` — List recent files for the running Manage year, in File menu order.
* `nwd_open_file` *(Write)* — Open a file. Refuses while the current file has unsaved changes unless `discard_changes` is true.
* `nwd_import_model` *(Write)* — Bring a model into the open document. `append` (default) adds it; `merge` combines it.

### 3. Query/Read Tools (8)
* `nwd_health_check` — Check active session status and heartbeat.
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
* `nwd_send_code` *(Write, on by default)* — Run a C# script on the Navisworks UI thread with `doc` as the active document. Returns the last expression as `result` and Console output as `stdout`; Navisworks waits while it runs.

### 9. ToolBaker Governed Tools (6)
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
- The total read-only tool surface is exactly **21 tools**. `nwd_list_recent_files` stays; `nwd_open_file` and `nwd_import_model` do not.

### Send code
`nwd_send_code` is **on by default**. `--read-only` still removes it. Turn it off with `--disable-send-code` or `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0` on the server, and `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` in the Navisworks process. Either side off blocks execution. `--enable-send-code` or `=1` turns it back on. An unrecognized value is off.

### Tool-call record
**Record** is **off by default**. The ribbon toggle, the Status checkbox, `recordCalls` in `nwdmcp.config.json`, and `BIMWRIGHT_NWD_RECORD_CALLS` share one switch for every tool. While it is on, each call is appended to `mcp-calls.jsonl` next to that config file. The `send_code` script is not written; the line stores `code_length` and `code_sha256`.

### Permissions & auto mode
Every tool except `nwd_send_code` declares MCP annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint=false`). Claude Desktop uses them: read tools can be allowed once, and destructive tools (`nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`) always ask.

Claude Code's auto mode checks each MCP call with a classifier. To skip that check for the read-only tools, add them to `permissions.allow` in `.claude/settings.json` (replace `nwd-mcp` if your MCP entry uses another name):

```json
{
  "permissions": {
    "allow": [
      "mcp__nwd-mcp__nwd_list_recent_files",
      "mcp__nwd-mcp__nwd_list_available_targets",
      "mcp__nwd-mcp__nwd_get_current_target",
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
