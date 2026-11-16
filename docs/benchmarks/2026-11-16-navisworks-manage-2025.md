# nwd-mcp tool benchmark — Navisworks Manage 2025

Run on 2026-11-16 (UTC). Result: **34 / 35 tools passed**, 106 / 116 calls passed.

## Setup

| Item | Value |
|---|---|
| Tested build | nwd-mcp commit `ac0084a`, server 1.1.0+ac0084a751e0db7f200e52500d1e8289478e4d92 |
| Host | Navisworks Manage 2025, plug-in over TCP loopback |
| Tool surface | 35 tools (full surface, send_code on) |
| Driven by | Claude Opus 5.5 (Claude Code), calling each tool through the MCP server over stdio |
| Project | Anonymised. Model file 31.0 MB (.nwc), 10,717 model items, 1 model(s), 11 saved viewpoints, 0 selection/search sets. Chosen as the largest existing file in the Navisworks recent list. |
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
| 27 | code | `nwd_send_code` | 5 | Failed | [ ] |
| 28 | toolbaker | `nwd_list_baked_tools` | 1 | Success | [x] |
| 29 | toolbaker | `nwd_list_bake_suggestions` | 1 | Success | [x] |
| 30 | toolbaker | `nwd_create_bake_issue_draft` | 1 | Success | [x] |
| 31 | toolbaker_write | `nwd_run_baked_tool` | 1 | Success | [x] |
| 32 | toolbaker_write | `nwd_accept_bake_suggestion` | 1 | Success | [x] |
| 33 | toolbaker_write | `nwd_dismiss_bake_suggestion` | 1 | Success | [x] |
| 34 | meta | `nwd_report_task_result` | 2 | Success | [x] |
| 35 | meta | `nwd_get_report_history` | 2 | Success | [x] |

## Results

| Tool | Workload | n | Cold ms | Warm p50 ms | Result size (bytes) | Est. tokens | Status | Size policy | Output file |
|---|---|---:|---:|---:|---:|---:|---|---|---|
| `nwd_list_recent_files` | limit 25 | 3 | 51.0 | 36.4 | 2,477 | 620 | Success | none | No |
| `nwd_open_file` | open model (discard_changes) | 1 | 201.2 | — | 238 | 60 | Success | none | No |
| `nwd_list_available_targets` | default | 3 | 3.0 | 1.1 | 436 | 109 | Success | none | No |
| `nwd_get_current_target` | default | 3 | 1.0 | 0.9 | 741 | 186 | Success | none | No |
| `nwd_switch_target` | same target, verify=true | 3 | 205.4 | 8.8 | 1,015 | 254 | Success | none | No |
| `nwd_health_check` | default | 3 | 11.6 | 8.1 | 3,620 | 905 | Success | none | No |
| `nwd_get_document_info` | default | 3 | 6.6 | 5.5 | 152 | 38 | Success | none | No |
| `nwd_get_model_statistics` | default | 3 | 81.6 | 297.5 | 91 | 23 | Success | none | No |
| `nwd_get_model_tree` | depth 2, cap 500 | 3 | 16.8 | 9.8 | 2,705 | 677 | Success | none | No |
| `nwd_get_model_tree` | depth 6, cap 5000 | 3 | 351.1 | 215.8 | 491,294 | 122,824 | Success | strong warning (>256 KiB) | No |
| `nwd_get_model_tree` | full tree, inline (budget test) | 1 | 429.8 | — | 274 | 69 | Success (expected `RESPONSE_TOO_LARGE`) | rejected (>1 MiB) | No |
| `nwd_get_model_tree` | full tree, output=file | 1 | 438.0 | — | 61,767 | 15,442 | Success | none | Yes (json, 1,056,091 bytes on disk) |
| `nwd_find_items_by_name` | name contains 'e', cap 500 | 3 | 29.4 | 17.9 | 8,166 | 2,042 | Success | none | No |
| `nwd_find_items_by_name` | name contains 'e', no cap, inline | 1 | 354.0 | — | 86,440 | 21,610 | Success | warning (>=64 KiB) | No |
| `nwd_find_items_by_name` | name contains 'e', no cap, output=file | 1 | 354.4 | — | 57,344 | 14,336 | Success | none | Yes (ndjson, 472,492 bytes on disk) |
| `nwd_find_items` | Name contains 'e', cap 500 | 3 | 12.3 | 4.3 | 19 | 5 | Success | none | No |
| `nwd_get_item_properties` | 1 item | 3 | 12.7 | 4.7 | 636 | 159 | Success | none | No |
| `nwd_batch_get_properties` | 10 ids | 3 | 30.0 | 15.4 | 21,798 | 5,450 | Success | none | No |
| `nwd_batch_get_properties` | 100 ids | 3 | 55.4 | 66.7 | 178,855 | 44,714 | Success | warning (>=64 KiB) | No |
| `nwd_batch_get_properties` | 500 ids, inline (budget test) | 1 | 859.4 | — | 269 | 68 | Success (expected `RESPONSE_TOO_LARGE`) | rejected (>1 MiB) | No |
| `nwd_batch_get_properties` | 500 ids, output=file | 1 | 2,182.5 | — | 6,934 | 1,734 | Success | none | Yes (sqlite, 2,093,056 bytes on disk) |
| `nwd_get_model_tree` | invalid output value (error path) | 1 | 4.5 | — | 109 | 28 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_get_current_selection` | empty selection | 3 | 9.0 | 9.4 | 15 | 4 | Success | none | No |
| `nwd_select_items_by_search` | Name contains 'e' | 3 | 18.9 | 16.4 | 20 | 5 | Success | none | No |
| `nwd_get_current_selection` | after search select | 3 | 8.1 | 6.5 | 19 | 5 | Success | none | No |
| `nwd_clear_selection` | default | 3 | 14.6 | 7.1 | 16 | 4 | Success | none | No |
| `nwd_list_sets` | default | 3 | 11.6 | 6.0 | 11 | 3 | Success | none | No |
| `nwd_get_selection_set_items` | model has no sets (error path) | 1 | 13.5 | — | 88 | 22 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_execute_search_set` | model has no sets (error path) | 1 | 9.6 | — | 88 | 22 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_list_viewpoints` | default | 3 | 12.8 | 6.2 | 1,546 | 387 | Success | none | No |
| `nwd_get_current_viewpoint` | default | 3 | 11.0 | 6.9 | 237 | 60 | Success | none | No |
| `nwd_goto_viewpoint` | first saved viewpoint | 3 | 10.5 | 462.9 | 14 | 4 | Success | none | No |
| `nwd_save_viewpoint` | adds a viewpoint | 1 | 12.3 | — | 237 | 60 | Success | none | No |
| `nwd_hide_items` | 1 id | 3 | 13.9 | 6.8 | 119 | 30 | Success | none | No |
| `nwd_hide_items` | 100 ids | 3 | 8.3 | 12.4 | 121 | 31 | Success | none | No |
| `nwd_unhide_all` | default | 3 | 7.9 | 7.2 | 106 | 27 | Success | none | No |
| `nwd_import_model` | append the same model | 1 | 1,637.9 | — | 242 | 61 | Success | none | No |
| `nwd_send_code` | return a scalar | 3 | 3,162.1 | 356.8 | 965 | 242 | Failed (`DOCUMENT_CHANGED`) | none | No |
| `nwd_send_code` | walk every item | 3 | 689.0 | 352.4 | 965 | 242 | Failed (`DOCUMENT_CHANGED`) | none | No |
| `nwd_send_code` | 2 MiB result (auto-spill test) | 1 | 429.2 | — | 965 | 242 | Failed (`DOCUMENT_CHANGED`) | none | No |
| `nwd_send_code` | compile error (error path) | 1 | 91.1 | — | 112 | 28 | Success (expected `SCRIPT_COMPILE_ERROR`) | none | No |
| `nwd_send_code` | data + task_v1 Pass (object result) | 3 | 776.1 | 375.1 | 965 | 242 | Failed (`DOCUMENT_CHANGED`) | none | No |
| `nwd_list_baked_tools` | default | 3 | 26.1 | 1.8 | 471 | 118 | Success | none | No |
| `nwd_list_bake_suggestions` | default | 3 | 11.4 | 1.1 | 206 | 52 | Success | none | No |
| `nwd_create_bake_issue_draft` | unknown id (error path) | 1 | 3.1 | — | 80 | 20 | Success (expected `not_found`) | none | No |
| `nwd_run_baked_tool` | unknown tool (error path) | 1 | 1.6 | — | 90 | 23 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_accept_bake_suggestion` | unknown id (error path) | 1 | 2.2 | — | 80 | 20 | Success (expected `not_found`) | none | No |
| `nwd_dismiss_bake_suggestion` | unknown id (error path) | 1 | 1.1 | — | 80 | 20 | Success (expected `not_found`) | none | No |
| `nwd_report_task_result` | report with body | 1 | 21.9 | — | 231 | 58 | Success | none | No |
| `nwd_report_task_result` | invalid outcome (error path) | 1 | 0.5 | — | 108 | 27 | Success (expected `INVALID_ARGUMENT`) | none | No |
| `nwd_get_report_history` | list newest 5 | 3 | 8.1 | 0.8 | 347 | 87 | Success | none | No |
| `nwd_get_report_history` | one report by id | 3 | 1.5 | 0.6 | 2,163 | 541 | Success | none | No |

## Checks

- MCP `isError` matched the result's `ok` flag on 116 of 116 calls.
- Overall: see Failed rows above.
