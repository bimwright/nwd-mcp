# Navisworks MCP — Completion Walkthrough

## Summary

This phase completed the half-finished Navisworks plug-in handler layer. All 16 domain
command handlers in `src\shared\Handlers\` now contain real Navisworks API calls instead
of shell stubs returning fabricated data. A unified `ModelItem` ID encoding scheme was
introduced and applied consistently across every handler.

## What Changed

### New: `ModelItemHelper.cs`
A shared helper providing deterministic ModelItem ID encoding and resolution.
IDs use a colon-separated index path (`modelIndex:childIndex:childIndex...`) that is
stable, fast, and does not depend on GUIDs or display names.

### Implemented (10 handlers — previously shells)

| Handler | API Surface |
|---|---|
| `HideItemsHandler` | `doc.Models.SetHidden(items, hide)` |
| `UnhideAllHandler` | `doc.Models.ResetAllHidden()` |
| `SelectItemsBySearchHandler` | `Search` + `doc.CurrentSelection.CopyFrom()` |
| `SaveViewpointHandler` | `SavedViewpoint` + `doc.SavedViewpoints.AddCopy()` |
| `GotoViewpointHandler` | Recursive find + `doc.CurrentViewpoint.CopyFrom()` |
| `ListViewpointsHandler` | Recursive walk of `doc.SavedViewpoints.RootItem` |
| `GetCurrentViewpointHandler` | Camera position, rotation, projection from `doc.CurrentViewpoint` |
| `ListSetsHandler` | Recursive walk of `doc.SelectionSets.RootItem` |
| `GetSelectionSetItemsHandler` | Set lookup + `ModelItemHelper.GetModelItemId()` |
| `ExecuteSearchSetHandler` | `SearchSet.Search.FindAll()` + optional selection |

### Updated for ID Consistency (6 read handlers)

| Handler | Change |
|---|---|
| `FindItemsHandler` | Uses `ModelItemHelper` for output IDs |
| `FindItemsByNameHandler` | Uses `ModelItemHelper` for output IDs |
| `GetModelTreeHandler` | Assigns `ModelItemHelper` IDs to tree nodes |
| `GetItemPropertiesHandler` | Resolves target item via `ModelItemHelper` ID |
| `BatchGetPropertiesHandler` | Resolves multiple items via `ModelItemHelper` IDs |
| `GetCurrentSelectionHandler` | Uses `ModelItemHelper` for selection IDs |

### Cleanups
- Deleted `tests\Bimwright.Nwd.Tests\UnitTest1.cs` (empty template file).
- Server builds with 0 warnings in both Debug and Release configurations.

## What Is Verified

| Gate | Status |
|---|---|
| `dotnet build` server Debug | ✅ 0 warnings, 0 errors |
| `dotnet build` server Release | ✅ 0 warnings, 0 errors |
| `dotnet test` (40 tests) | ✅ All passing |
| No "Under real run" stubs | ✅ Verified by grep |
| `#if NAVIS20YY` guards | ✅ All handler code gated |
| Newtonsoft.Json only | ✅ No System.Text.Json in handlers |

## What Is NOT Verified

> **The plug-in handler code has not been compiled or run against a live Navisworks
> instance.** The build machine does not have Autodesk Navisworks Manage installed, so
> the `net48` plug-in projects cannot be compiled here. The handler implementations are
> written against the documented Navisworks .NET API and cross-referenced with working
> open-source implementations (Aitology/navisworks-mcp), but they require validation on
> a machine with Navisworks Manage 2022+ installed.
>
> **First-run checklist for a Navisworks machine:**
> 1. `dotnet build src\NwdMcp.sln -c Debug` — should compile all plug-in projects.
> 2. Load the plug-in in Navisworks Manage, open a model file.
> 3. Exercise each of the 21 domain commands via the MCP server.
> 4. Verify ModelItem IDs round-trip correctly (query → get properties → hide → unhide).
