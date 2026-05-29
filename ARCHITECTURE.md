# Bimwright Navisworks MCP Architecture

Bimwright Navisworks MCP is a professional-grade desktop integration framework exposing Autodesk Navisworks Manage automation through the Model Context Protocol (MCP).

---

## Two-Process System Model

The system operates across a clear process boundary:

```text
AI client (e.g. Claude Code)
  └─ Stdio
      ▼
.NET 8 MCP Server (Bimwright.Nwd.Server)
  └─ localhost TCP NDJSON (Local port + token auth)
      ▼
Autodesk Navisworks Manage (net48 In-Process Plug-In)
  └─ Navisworks UI Thread Invoker
      ▼
Autodesk.Navisworks.Api
```

1. **MCP stdio Server (`Bimwright.Nwd.Server`):** A modern, lightweight `.NET 8` console application that AI clients launch over stdin/stdout. It translates MCP tool calls into NDJSON TCP packets, routes them, and performs response-size auditing.
2. **Navisworks Plug-in (`net48`):** A single-threaded in-process plug-in (`Bimwright.Nwd.Plugin.NavisYY` where `YY` is `22..27`) running inside Navisworks Manage. It runs a localhost-only TCP listener, marshals socket payloads to the main UI thread via the `NavisworksUiThreadInvoker`, dispatches actions to concrete `INwdCommand` handlers, and returns results.

---

## Plug-In Framework versioning
All six version-specific plug-in shells (2022 to 2027) target `.NET Framework 4.8` (`net48`). 

Unlike Autodesk Revit or AutoCAD which transitioned to .NET 8 at version 2025, Navisworks Manage has retained `.NET Framework 4.8` for all versions including 2026/2027. (See official [Autodesk Developer Forums and SDK release notes](https://blog.autodesk.io/navisworks-2024-sdk-is-posted/) confirming .NET Framework 4.8 requirements). 

---

## Source Layering and Single-Solution Strategy

To support robust compiling and testing on machines **without Navisworks installed**, the project uses strict layering:

| Layer | Folder(s) | Navisworks API? | Compiled Into |
|---|---|---|---|
| **API-Free Shared** | `src/shared/Infrastructure/`, `src/shared/Security/` | No | Server (selective links) + Plug-ins + Tests |
| **Navisworks-API Shared** | `src/shared/Handlers/`, `src/shared/Plugin/`, `src/shared/Transport/` | Yes | Plug-ins only |
| **Server-only** | `src/server/` | No | Server only |
| **Plug-in-only** | `src/plugin-navisYY/` | Yes | Plug-in YY only |

### Single Solution (`NwdMcp.sln`)
A single Visual Studio solution `NwdMcp.sln` aggregates the server, tests, and all six version plug-in projects.
- **CI / Server Build:** Targets `src/server/Bimwright.Nwd.Server.csproj` and `tests/Bimwright.Nwd.Tests/Bimwright.Nwd.Tests.csproj` specifically. Since they only link API-free shared source, they compile and run on any machine without Navisworks.
- **Plug-in Build:** Requires a local Navisworks Manage install to satisfy assemblies references. Plug-in compilation is explicitly deferred to machines equipped with Navisworks.

---

## Session Discovery & Multi-Instance Routing

Discovery uses local JSON descriptors written to the file system, supporting **multiple running Navisworks instances** at once.

### The Descriptor File
When a plug-in initializes inside Navisworks Manage, it generates a cryptographically random 32-byte hexadecimal authentication token (`auth_token`), binds to a random open TCP port, and writes a descriptor file under:
```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\navis-<year>-<pid>.json
```

The descriptor exposes:
```json
{
  "target_id": "navis-2026-12345",
  "navisworks_year": 2026,
  "process_id": 12345,
  "port": 48592,
  "auth_token": "8f2a...c5e2",
  "document_title": "warehouse.nwd",
  "last_heartbeat_utc": "2026-05-29T14:30:00Z"
}
```

### Liveness Algorithm
The server's `TargetRegistry` lists and connects to active targets. A target is considered **live** if:
1. `host_product` is exactly `"Manage"`.
2. `navisworks_year` is in the range `2022` to `2027`.
3. The `process_id` matches an active running OS process.
4. The `last_heartbeat_utc` timestamp is fresh (within 120 seconds).
