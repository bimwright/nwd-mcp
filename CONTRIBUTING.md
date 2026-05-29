# Contributing to Bimwright Navisworks MCP

Thanks for your interest. Bimwright is a professional-grade MCP gateway for Navisworks Manage. Open an issue before a large PR so we can agree on scope.

## Dev setup

### Prereqs

- Windows 10/11 (Navisworks Manage is Windows-only).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — required for the server and tests.
- .NET Framework 4.8 Developer Pack — required to compile the net48 plugin projects.
- Visual Studio 2022+ or JetBrains Rider.
- One or more Navisworks Manage installations (2022–2027) for plugin compile and runtime testing.

To compile and run tests on the server *only*, no Navisworks installation is required.

### Clone + build

```bash
git clone https://github.com/bimwright/nwd-mcp.git
cd nwd-mcp
dotnet build src/server/Bimwright.Nwd.Server.csproj -c Debug
```

**Close every running Navisworks Manage before building the plugin.** Plugin DLLs deploy to `%APPDATA%\Autodesk\ApplicationPlugins\Bimwright.Nwd.bundle\` as part of the build, and Navisworks holds file locks on loaded plugins.

Build output lands in `src/plugin-navisYY/bin/Debug/` and `src/server/bin/Debug/net8.0/`.

### Run tests

```bash
dotnet test tests/Bimwright.Nwd.Tests/Bimwright.Nwd.Tests.csproj -c Debug
```

Tests are pure .NET 8 xUnit, no Navisworks dependency — they cover configuration, toolset filtering, transport, dynamic gating, and schema snapshots. Anything that needs a live Navisworks document is tested manually.

## Project layout

See [ARCHITECTURE.md](ARCHITECTURE.md) for the conceptual model. Quick reference:

| Path | What lives here |
|------|-----------------|
| `src/server/` | MCP server, tool registration, stdio entry points |
| `src/shared/Handlers/` | One file per Navisworks command handler; 21 MCP domain commands |
| `src/shared/Infrastructure/` | `CommandDispatcher`, `INwdCommand`, `NwdCommandContext`, `NwdCommandCatalog`, `TargetRegistry` |
| `src/shared/Transport/` | TCP listener NDJSON transport |
| `src/shared/Security/` | `SecretMasker`, `ErrorSanitizer` |
| `src/shared/ToolBaker/` | Roslyn-based self-evolution engine |
| `src/plugin-navisYY/` | Navisworks version shells (2022-2027) |
| `tests/Bimwright.Nwd.Tests/` | xUnit tests (pure .NET 8, no Navisworks API) |
| `scripts/` | `install-bundle.ps1`, `PackageContents.template.xml` |

## Adding a new MCP tool

1. Write the handler in `src/shared/Handlers/<Verb><Noun>Handler.cs` implementing `INwdCommand`. Return DTOs (anonymous objects or `JObject`) — never serialize Navisworks API objects directly.
2. Register in `src/shared/Plugin/NwdCommandRegistry.cs`: `Add(new YourHandler());`.
3. Add the command name to `src/shared/Plugin/NwdCommandRegistry.Names.cs` and `src/shared/Infrastructure/NwdCommandCatalog.cs`.
4. Add an `[McpServerTool]` method in the matching toolset class under `src/server/Tools/` (e.g., `QueryTools.cs`).
5. Cover any non-trivial logic with an xUnit test in `tests/Bimwright.Nwd.Tests/`.
6. Manual smoke test in at least one Navisworks version before PR.

## Coding style

- Match the surrounding code. Existing handlers are the authoritative reference.
- DTOs are anonymous objects or `JObject`s. Lowercase JSON property names — already the default Newtonsoft.Json contract.
- Comments explain *why*, not *what*. Identifiers explain *what*.

## Commit + PR

- One logical change per commit. Commit messages start with a short scope prefix (e.g. `handlers:`, `transport:`, `ci:`).
- Open a PR against `main`. CI must be green.
- Include a short "Tested with" line: which Navisworks Manage year(s) you smoke-tested.

## Code of Conduct

Be kind. Assume good faith. If that doesn't cover your situation, we'll default to [Contributor Covenant v2.1](https://www.contributor-covenant.org/version/2/1/code_of_conduct/).
