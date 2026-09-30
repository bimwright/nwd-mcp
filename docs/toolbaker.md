# ToolBaker Security & Architecture Model

`nwd-mcp` ships with the **ToolBaker** self-evolution platform. This allows AI agents to write, compile, register, and run reusable C# tools dynamically inside the Navisworks Manage plug-in process, governed by a multi-layered safety policy.

## The send_code Escape Hatch
`nwd_send_code` is the direct execution command. It compiles and evaluates raw C# code in-process using Roslyn Scripting. It is **on by default**. Both the server and the plug-in can turn it off; either side off blocks the call with `SEND_CODE_DISABLED` or by not registering the tool.

### Turning send_code off
1. **Server:** start with `--disable-send-code`, or set `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0`. `--enable-send-code` or `=1` turns it back on. `--read-only` removes it with the other write tools.
2. **Plug-in:** set `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` (also `false`, `no`, or `off`) in the Navisworks process. Unset means on. Any other value means off.

---

## ToolBaker Suggestions and Lifecycle
To prevent repetitive raw code executions and promote governance:
- **`nwd_list_bake_suggestions`** lists suggested tools based on recurring C# snippets.
- **`nwd_accept_bake_suggestion`** validates the source, compiles it into a DLL, registers it in `bake.db` under `%LOCALAPPDATA%\Bimwright\nwd-mcp\baked`, and deploys it as a governed reusable tool.
- **`nwd_run_baked_tool`** runs an accepted, compiled baked tool by name with safety checks.

---

## Compiler Safety Policy
Before any dynamic C# snippet is compiled by `ToolCompiler` or accepted into the registry, it is validated against the strict `BakeCompilerPolicy`.

### Banned APIs
The compilation is blocked if the source code references any of the following restricted capabilities:
- **Destructive file operations:** `File.Delete`, `Directory.Delete`
- **Process spawning:** `Process.Start`
- **System environment mutation:** `Environment.GetEnvironmentVariable`
- **External network access:** `HttpClient`, `System.Net`, `Socket`

---

## Dispatch Authorization allow/deny lists
When running a baked tool via `nwd_run_baked_tool`, execution must go through the `BakedToolDispatchAuthorizer`.

### Denied Targets
A baked tool **cannot** invoke the following commands:
- `send_code` (prevents self-execution loop)
- `batch_execute`
- `run_baked_tool` (prevents nested call stack overflow)
- `accept_bake_suggestion`
- `dismiss_bake_suggestion`
- `list_baked_tools`

### Allowed Commands
Baked tools are designed for model queries and calculations, using safe read/query commands:
- `find_items`
- `find_items_by_name`
- `get_item_properties`
- `get_model_tree`
- `list_sets`
- `execute_search_set`
