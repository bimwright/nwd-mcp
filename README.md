# nwd-mcp

`nwd-mcp` is a Bimwright MCP gateway for Autodesk Navisworks Manage automation.

Initial scope:

- Navisworks Manage desktop only.
- Versions 2022, 2023, 2024, 2025, 2026, and 2027 (all .NET Framework 4.8 plug-ins).
- MCP tools use the `nwd_*` prefix.
- The server builds and runs its tests without Navisworks installed.
- Plug-in compile and smoke require a local Navisworks install.

The first release slice contains 23 Navisworks domain/meta tools plus the Bimwright platform
toolsets for `nwd_send_code` and ToolBaker.
