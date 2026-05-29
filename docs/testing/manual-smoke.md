# Navisworks Manual Smoke Testing Checklist

Follow this numbered checklist on a machine with Autodesk Navisworks Manage installed to verify the plug-in, transport, and server integration.

1. **Install the Plugin Bundle**
   Run the local deployment script for your installed version:
   ```powershell
   powershell -File .\scripts\install-bundle.ps1
   ```
2. **Launch Navisworks Manage**
   Start Navisworks Manage (desktop version 2022–2027).
3. **Confirm Plug-In Initialization**
   Confirm that the plug-in loads without errors. Check that a session target descriptor file `navis-<year>-<pid>.json` is generated under:
   ```text
   %LOCALAPPDATA%\Bimwright\nwd-mcp\
   ```
4. **Start the MCP Server**
   Start the stdio MCP server in a separate terminal:
   ```powershell
   .\src\server\bin\Debug\net8.0\Bimwright.Nwd.Server.exe
   ```
5. **Verify Discovery**
   Call `nwd_list_available_targets` to verify it lists the running Navisworks Manage instance.
6. **Query Document Info**
   Open a sample model (e.g., `.nwd` or `.nwf`) and call `nwd_get_document_info`. Verify it reports the correct document title, file path, and model count.
7. **Query Model Tree**
   Call `nwd_get_model_tree` with `maxDepth=2`. Verify it returns a bounded hierarchy.
8. **Find Items**
   Call `nwd_find_items_by_name` with `name="Column"` (or another name from your model) and verify it returns matched element IDs.
9. **Get Current Selection**
   Select some elements in the Navisworks UI, and call `nwd_get_current_selection`. Confirm the matching element IDs are returned.
10. **Query Viewpoints**
    Call `nwd_list_viewpoints` and confirm that any saved viewpoints/folders are enumerated.
11. **Test Element Visibility**
    Call `nwd_hide_items` with a few element IDs, and confirm they disappear in the active viewport. Call `nwd_unhide_all` to confirm they are restored.
12. **Gated Code Safety**
    By default, verify that `nwd_send_code` is absent or returns an error.
13. **Enable send_code Gating**
    Start the server with `--enable-send-code` (or `BIMWRIGHT_NWD_ENABLE_SEND_CODE=1`) and set the plug-in's environment variable `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1` before starting Navisworks.
14. **Execute dynamic C#**
    Call `nwd_send_code` with a harmless script:
    ```csharp
    System.Console.WriteLine("Hello from Navisworks!");
    ```
    Confirm the response contains `ok: true` and the captured `stdout`.
15. **Query Baked Tools**
    Call `nwd_list_baked_tools` and verify it returns an initialized (empty or populated) registry from `bake.db`.
16. **Multi-Instance Routing**
    Open a SECOND Navisworks Manage instance. Call `nwd_list_available_targets` and confirm it lists BOTH instances. Verify that calling `nwd_switch_target` redirects subsequent commands to the chosen target.
