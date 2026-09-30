# nwd-mcp tool benchmark — Navisworks Manage 2025

Run on 2026-09-30 (UTC). Result: **33 / 33 tools passed**, 107 / 107 calls passed.

## Setup

| Item | Value |
|---|---|
| Tested build | nwd-mcp commit `72a7344`, server 0.1.2.0 |
| Host | Navisworks Manage 2025, plug-in over TCP loopback |
| Tool surface | 33 tools (full surface, send_code on) |
| Driven by | SWE-2 (effort High), calling each tool through the MCP server over stdio |
| Project | Anonymised. Model file 277.4 MB (.ifc), 62,739 model items, 1 model(s), 0 saved viewpoints, 202 selection/search sets. Chosen as the largest existing file in the Navisworks recent list. |
| CPU | 12 cores / 20 threads, up to 3.6 GHz (base clock reported by the OS) |
| RAM | 48 GB DDR4, 4 modules, 2133 MT/s |
| Mainboard | desktop mainboard, PCIe NVMe storage |
| GPU | discrete GPU, 8 GB VRAM |
| SSD | NVMe SSD, 1.0 TB |
| OS | Windows 11 Pro |

## How to read the numbers

- **Duration**: end-to-end milliseconds seen by the MCP client (server + loopback + Navisworks UI thread + handler). Repeatable calls ran 3 times: *cold* is the first call, *warm p50* the median of the rest.
- **Result size**: UTF-8 bytes of the tool result text the agent receives. Result content is never recorded here.
- **Est. tokens**: `ceil(bytes / 4)`, a rough rule of thumb for JSON text. Real counts depend on the model's tokenizer.
- **Status**: *Success* when the call returned its normal result, or — for rows marked *error path* or *budget test* — the error code the design calls for. *Failed* otherwise.
- **Size policy**: what the server's response-size policy did: `warning` adds `_response_warning` from 64 KiB, `strong warning` above 256 KiB, `rejected` past the 1 MiB budget (`RESPONSE_TOO_LARGE`), `compacted write` for a completed write that was too large.
- **Output file**: *Yes* when the result was written to the local spill folder and the file exists on disk (format and size on disk shown). `output=file` rows ask for it; `nwd_send_code` spills automatically above 1 MiB.
- Server defaults were used (plug-in timeout 30 s). The model was opened with `discard_changes` and reopened the same way at the end; nothing was saved.

## Coverage

| # | Toolset | Tool | Workloads | Status | Check |
|---:|---|---|---:|---|:---:|
| 1 | files | `nwd_list_recent_files` | 1 | Success | [x] |
| 2 | files_write | `nwd_open_file` | 1 | Success | [x] |
| 3 | meta | `nwd_list_available_targets` | 1 | Success | [x] |
| 4 | meta | `nwd_get_current_target` | 1 | Success | [x] |
| 5 | meta | `nwd_switch_target` | 1 | Success | [x] |
| 6 | query | `nwd_health_check` | 1 | Success | [x] |
| 7 | query | `nwd_get_document_info` | 1 | Success | [x] |
| 8 | query | `nwd_get_model_statistics` | 1 | Success | [x] |
| 9 | query | `nwd_get_model_tree` | 5 | Success | [x] |
| 10 | query | `nwd_find_items_by_name` | 3 | Success | [x] |
| 11 | query | `nwd_find_items` | 1 | Success | [x] |
| 12 | query | `nwd_get_item_properties` | 1 | Success | [x] |
| 13 | query | `nwd_batch_get_properties` | 4 | Success | [x] |
| 14 | selection | `nwd_get_current_selection` | 2 | Success | [x] |
| 15 | selection_write | `nwd_select_items_by_search` | 1 | Success | [x] |
| 16 | selection_write | `nwd_clear_selection` | 1 | Success | [x] |
| 17 | sets | `nwd_list_sets` | 1 | Success | [x] |
| 18 | sets | `nwd_get_selection_set_items` | 1 | Success | [x] |
| 19 | sets | `nwd_execute_search_set` | 1 | Success | [x] |
| 20 | view | `nwd_list_viewpoints` | 1 | Success | [x] |
| 21 | view | `nwd_get_current_viewpoint` | 1 | Success | [x] |
| 22 | view_write | `nwd_goto_viewpoint` | 1 | Success | [x] |
| 23 | view_write | `nwd_save_viewpoint` | 1 | Success | [x] |
| 24 | visibility | `nwd_hide_items` | 2 | Success | [x] |
| 25 | visibility | `nwd_unhide_all` | 1 | Success | [x] |
| 26 | files_write | `nwd_import_model` | 1 | Success | [x] |
| 27 | code | `nwd_send_code` | 4 | Success | [x] |
| 28 | toolbaker | `nwd_list_baked_tools` | 1 | Success | [x] |
| 29 | toolbaker | `nwd_list_bake_suggestions` | 1 | Success | [x] |
| 30 | toolbaker | `nwd_create_bake_issue_draft` | 1 | Success | [x] |
| 31 | toolbaker_write | `nwd_run_baked_tool` | 1 | Success | [x] |
| 32 | toolbaker_write | `nwd_accept_bake_suggestion` | 1 | Success | [x] |
| 33 | toolbaker_write | `nwd_dismiss_bake_suggestion` | 1 | Success | [x] |

## Results

| Tool | Workload | n | Cold ms | Warm p50 ms | Result size (bytes) | Est. tokens | Status | Size policy | Output file |
|---|---|---:|---:|---:|---:|---:|---|---|---|
| `nwd_list_recent_files` | limit 25 | 3 | 134.0 | 29.1 | 491 | 123 | Success | none | No |
| `nwd_open_file` | open model (discard_changes) | 1 | 297.4 | — | 236 | 59 | Success | none | No |
| `nwd_list_available_targets` | default | 3 | 8.6 | 1.1 | 117 | 30 | Success | none | No |
| `nwd_get_current_target` | default | 3 | 0.8 | 0.9 | 115 | 29 | Success | none | No |
| `nwd_switch_target` | default | 3 | 3.0 | 0.9 | 42 | 11 | Success | none | No |
| `nwd_health_check` | default | 3 | 1,004.5 | 30.3 | 122 | 31 | Success | none | No |
| `nwd_get_document_info` | default | 3 | 10.8 | 8.9 | 156 | 39 | Success | none | No |
| `nwd_get_model_statistics` | default | 3 | 2,254.9 | 3,676.0 | 91 | 23 | Success | none | No |
| `nwd_get_model_tree` | depth 2, cap 500 | 3 | 52.0 | 17.5 | 305 | 77 | Success | none | No |
| `nwd_get_model_tree` | depth 6, cap 5000 | 3 | 28.1 | 24.2 | 20,436 | 5,109 | Success | none | No |
| `nwd_get_model_tree` | full tree, inline (budget test) | 1 | 2,877.1 | — | 274 | 69 | Success (expected `RESPONSE_TOO_LARGE`) | rejected (>1 MiB) | No |
| `nwd_get_model_tree` | full tree, output=file | 1 | 3,163.3 | — | 58,595 | 14,649 | Success | none | Yes (json, 5,726,998 bytes on disk) |
| `nwd_find_items_by_name` | name contains 'e', cap 500 | 3 | 82.1 | 171.7 | 9,424 | 2,356 | Success | none | No |
| `nwd_find_items_by_name` | name contains 'e', no cap, inline | 1 | 2,709.3 | — | 125,064 | 31,266 | Success | warning (>=64 KiB) | No |
| `nwd_find_items_by_name` | name contains 'e', no cap, output=file | 1 | 2,668.6 | — | 57,152 | 14,288 | Success | none | Yes (ndjson, 632,777 bytes on disk) |
| `nwd_find_items` | Name contains 'e', cap 500 | 3 | 40.0 | 8.1 | 19 | 5 | Success | none | No |
| `nwd_get_item_properties` | 1 item | 3 | 24.7 | 11.1 | 3,336 | 834 | Success | none | No |
| `nwd_batch_get_properties` | 10 ids | 3 | 16.3 | 20.0 | 20,653 | 5,164 | Success | none | No |
| `nwd_batch_get_properties` | 100 ids | 3 | 126.0 | 142.4 | 295,574 | 73,894 | Success | strong warning (>256 KiB) | No |
| `nwd_batch_get_properties` | 500 ids, inline (budget test) | 1 | 1,123.5 | — | 269 | 68 | Success (expected `RESPONSE_TOO_LARGE`) | rejected (>1 MiB) | No |
| `nwd_batch_get_properties` | 500 ids, output=file | 1 | 1,289.1 | — | 7,737 | 1,935 | Success | none | Yes (sqlite, 1,683,456 bytes on disk) |
| `nwd_get_model_tree` | invalid output value (error path) | 1 | 3.0 | — | 109 | 28 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_get_current_selection` | empty selection | 3 | 9.6 | 7.5 | 15 | 4 | Success | none | No |
| `nwd_select_items_by_search` | Name contains 'e' | 3 | 15.4 | 33.2 | 20 | 5 | Success | none | No |
| `nwd_get_current_selection` | after search select | 3 | 46.5 | 10.3 | 19 | 5 | Success | none | No |
| `nwd_clear_selection` | default | 3 | 11.4 | 18.8 | 16 | 4 | Success | none | No |
| `nwd_list_sets` | default | 3 | 14.8 | 10.2 | 19,987 | 4,997 | Success | none | No |
| `nwd_get_selection_set_items` | first set | 3 | 13.0 | 13.7 | 81 | 21 | Success | none | No |
| `nwd_execute_search_set` | first set, select=false | 3 | 14.1 | 18.4 | 116 | 29 | Success | none | No |
| `nwd_list_viewpoints` | default | 3 | 8.7 | 9.2 | 17 | 5 | Success | none | No |
| `nwd_get_current_viewpoint` | default | 3 | 13.0 | 5.2 | 239 | 60 | Success | none | No |
| `nwd_goto_viewpoint` | model has no viewpoints (error path) | 1 | 10.0 | — | 106 | 27 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_save_viewpoint` | adds a viewpoint | 1 | 12.9 | — | 14 | 4 | Success | none | No |
| `nwd_hide_items` | 1 id | 3 | 41.6 | 7.3 | 30 | 8 | Success | none | No |
| `nwd_hide_items` | 100 ids | 3 | 14.9 | 24.2 | 32 | 8 | Success | none | No |
| `nwd_unhide_all` | default | 3 | 23.3 | 13.1 | 17 | 5 | Success | none | No |
| `nwd_import_model` | append the same model | 1 | 1,198.5 | — | 147 | 37 | Success | none | No |
| `nwd_send_code` | return a scalar | 3 | 1,018.2 | 462.1 | 48 | 12 | Success | none | No |
| `nwd_send_code` | walk every item | 3 | 524.4 | 458.4 | 51 | 13 | Success | none | No |
| `nwd_send_code` | 2 MiB result (auto-spill test) | 1 | 502.7 | — | 51,856 | 12,964 | Success | none | Yes (json, 2,097,200 bytes on disk, automatic) |
| `nwd_send_code` | compile error (error path) | 1 | 11.2 | — | 112 | 28 | Success (expected `SCRIPT_COMPILE_ERROR`) | none | No |
| `nwd_list_baked_tools` | default | 3 | 4.8 | 1.8 | 12 | 3 | Success | none | No |
| `nwd_list_bake_suggestions` | default | 3 | 1.9 | 0.9 | 18 | 5 | Success | none | No |
| `nwd_create_bake_issue_draft` | unknown id (error path) | 1 | 2.2 | — | 80 | 20 | Success (expected `not_found`) | none | No |
| `nwd_run_baked_tool` | unknown tool (error path) | 1 | 1.5 | — | 90 | 23 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_accept_bake_suggestion` | unknown id (error path) | 1 | 2.0 | — | 80 | 20 | Success (expected `not_found`) | none | No |
| `nwd_dismiss_bake_suggestion` | unknown id (error path) | 1 | 1.0 | — | 80 | 20 | Success (expected `not_found`) | none | No |

## Checks

- MCP `isError` matched the result's `ok` flag on 107 of 107 calls.
- Overall: all calls behaved as designed.
