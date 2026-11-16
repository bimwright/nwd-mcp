<!-- agents-install-guide -->
<!-- mcp-name: io.github.bimwright/nwd-mcp -->

# AGENTS.md — nwd-mcp install guide for AI agents

This file is machine-readable install instructions for AI coding agents. A user pointed you here because they want nwd-mcp (a Navisworks Manage MCP server) wired up without hand-editing more than the client entry.

**What you can do:**

- Install the client setup ZIP from the latest GitHub Release (`NwdMcp.Setup-*-win-x64.zip`).
- Deploy the bundled add-in to `%APPDATA%\Autodesk\ApplicationPlugins\Bimwright.Nwd.bundle\` and the server to `%LOCALAPPDATA%\Bimwright\nwd-mcp\server\current\nwd-mcp.exe`.
- Point the MCP client at that executable. The installer does not edit client config files.
- Verify `tools/list` and `nwd_health_check` after the client restarts. For an upgrade failure, restore the previous bundle/server backups; do not use uninstall as an upgrade rollback.

**Current status — 2026-10-02:** the latest published release is **v1.0.0**. Use `/repos/bimwright/nwd-mcp/releases/latest`. v1.0.1 is not published. The v1.0.0 ZIP ships the Manage **2025** plug-in only.

**What you cannot do:**

- Install Navisworks, manage its license, or launch it for the first time. If Navisworks Manage 2022–2027 is not installed, stop and tell the user.
- Use NuGet as the client installer. `Bimwright.Nwd.Server` is server-only and has no plug-in. Run `dotnet tool install -g Bimwright.Nwd.Server` only when the user explicitly asked for the global tool `bimwright-nwd` and the plug-in is already installed from the ZIP.
- Clone, restore, or build source for a client install unless the user explicitly asked for a developer setup.

## Rules

1. Preview with `-WhatIf` before `install.ps1` writes anything. Show the user the paths.
2. Close Navisworks before installing or uninstalling. It locks the add-in DLLs.
3. Do not edit an MCP client config until the user has seen the exact diff. The installer never does this for you.
4. Do not allow `mcp__nwd-mcp__*`. That wildcard also approves `nwd_send_code` and every write tool.
5. After wiring, restart the client, then verify `initialize`, `tools/list` and `nwd_health_check` on the same new connection. Do not rely on tool count alone. A default install lists 35 tools; `--read-only` lists 25 and removes `nwd_send_code`.

## Session startup and version checks

At the start of each Navisworks session, select the intended target if multiple instances are present, then call `nwd_health_check(check_updates=true)` once to discover newer stable releases. If network access is unavailable or disallowed, use the offline default and report not checked/unknown. Later routine probes stay offline. Require `host_connected=true` to verify the host connection; a target-registry entry alone is not proof. It reports document state and loaded server/plugin versions, build ids and version/revision alignment. No target still returns the server identity with `ok:false` and `host_connected:false`. A legacy plugin without identity stays connected but its version/build evidence is `unknown`. The host probe uses the UI thread and can time out while Navisworks is busy.

Preserve `setup_gateway` and `setup_plugin`. Informational version and MVID describe loaded assemblies; `artifact_sha256` observes the disk file at identity capture, not loaded memory. Copying a file to disk does not prove the process reloaded it. Alignment is not proof of compatibility or publisher trust.

Use `check_updates=true` to compare `update.components.server` and `.plugin` with one official stable release and its tag revision (5-second shared network budget). Inspect both component statuses: `update.status=checked` only confirms release lookup; unavailable evidence remains `unknown`. Equal versions alone cannot prove a release build match, and revision comparison does not verify artifact bytes.

Report `update.notification` code/message according to `agent_action`: `NEW_RELEASE_AVAILABLE` reports a newer version; `RELEASE_REVIEW_REQUIRED`/`RELEASE_IDENTITY_UNVERIFIED` request evidence review; `LOCAL_VERSION_AHEAD` neutrally identifies newer local components. `UPDATE_CHECK_UNAVAILABLE` reports the external `lookup_reason`, not a local runtime fault, and must not prompt for installation authorization. Preserve `protected_components`, explain `protected_component_reasons`, and never downgrade an `ahead_of_release` component. Treat `unobserved_components` separately: obtain the intended Navisworks connection/runtime metadata without inferring corruption or that Navisworks is closed.

Follow `update_guidance.mode`: only `prepare_update` with `bundle_update_proposal_allowed=true` contains installation steps and needs installation authorization, reusing existing permission. Other modes (`check_only`, `retry_check`, `review`, `none`) contain only check/report/review actions. Null notification or semantic `update_available=false` does not prove release identity. Health never downloads packages, installs, saves/closes documents or restarts processes. Do not replace development builds automatically.

For a permitted update proposal (`prepare_update`), use the official Setup ZIP, preview `install.ps1 -WhatIf`, and obtain or reuse approval. Close Navisworks only with the user's involvement. Back up the installed bundle and server first: the current installer does not provide an atomic bundle+server rollback. Preserve client configs/personal data; restore those backups if the upgrade fails rather than uninstalling.

After updating, restart Navisworks and reconnect/restart the MCP client. On the **same new connection**, record `initialize.serverInfo` and fresh `tools/list`, then call health. Compare server name/full informational version with `client_verification.expected_server_info`; compare the health tool's `_meta["bimwright/nwd-mcp/runtime"]` with `client_verification.expected_runtime` (version, MVID, PID and start time). Verify schema/annotations too: optional boolean `check_updates`, read-only true, destructive false, open-world true. The server reports `not_observable_by_server`, not a client-refresh pass. If the client cannot expose handshake/catalog evidence, record verification as unverified; a direct-server probe does not replace it.

## Prerequisites

| Requirement | How to check | If missing |
|---|---|---|
| Windows | `[System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform('Windows')` | Stop. Navisworks is Windows-only. |
| Navisworks Manage 2022–2027 | The Manage install for the year in the ZIP | Tell the user. You cannot install it. |
| PowerShell 5.1 or later | `$PSVersionTable.PSVersion` | Stop and say so. |

## Install

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/nwd-mcp/releases/latest).tag_name
$zip = "$env:TEMP\NwdMcp.Setup-$tag-win-x64.zip"
$dir = "$env:TEMP\NwdMcp.Setup-$tag-win-x64"
Invoke-WebRequest "https://github.com/bimwright/nwd-mcp/releases/download/$tag/NwdMcp.Setup-$tag-win-x64.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force
powershell -ExecutionPolicy Bypass -File "$dir\install.ps1" -WhatIf
powershell -ExecutionPolicy Bypass -File "$dir\install.ps1"
```

Restart Navisworks Manage. The summary line `MCP command:` is the executable to register. Older versioned copies under `server\<version>\` stay on disk; point the client at `server\current\nwd-mcp.exe`.

## Connect the client

Register one stdio entry. Do not add args unless the user asked for a flag such as `--read-only` or `--target`.

| Field | Value |
|---|---|
| Name | `nwd-mcp` |
| Transport | stdio |
| Command | `%LOCALAPPDATA%\Bimwright\nwd-mcp\server\current\nwd-mcp.exe` |

```json
{
  "mcpServers": {
    "nwd-mcp": {
      "command": "C:\\Users\\<user>\\AppData\\Local\\Bimwright\\nwd-mcp\\server\\current\\nwd-mcp.exe",
      "args": []
    }
  }
}
```

Restart the client after the edit. If this session is that client, the new tools appear only in the next session.

## Verify

With Navisworks Manage running, `tools/list` should include `nwd_health_check` and `nwd_get_document_info`. Call `nwd_health_check` with no arguments; require `host_connected=true` and inspect loaded server/plugin identities. A document is not required for health. Verify the handshake/catalog evidence described above. If verification fails, do not claim the install succeeded.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File "$dir\install.ps1" -Uninstall -WhatIf
powershell -ExecutionPolicy Bypass -File "$dir\install.ps1" -Uninstall
```

This removes the ApplicationPlugins bundle, the installed server copies, and the spill cache. It does not remove the MCP client entry or the rest of `%LOCALAPPDATA%\Bimwright\nwd-mcp\` (settings, call log, ToolBaker data). Remove the client entry yourself after the user agrees.
