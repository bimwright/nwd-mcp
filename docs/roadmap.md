# Bimwright Navisworks MCP Roadmap

This document outlines future release scopes, deferred domains, and future major version compatibility rules.

## Phase 2: Deferred Domains
The initial Phase 1 release establishes the two-process architecture and 21 core domain/meta tools. The following features are deferred and will be prioritized in Phase 2:

### 1. Clash Detection
- Full integration with `Autodesk.Navisworks.Api.Clash`.
- Tools to list clash tests, run tests, retrieve clash results/groups, and isolate clashing elements.

### 2. Measurements
- Retrieve point-to-point and point-to-line dimensions.
- Expose model clearance and distance measurements.

### 3. Model Export and Formats
- Automate exporting active files to NWD, DWF, and NWC.
- Automate publishing sheets and models with native parameters.

### 4. Autodesk Platform Services (APS) Integration
- Cloud upload/download pipelines.
- Headless automation compatibility for APS design automation for Navisworks.

---

## Future .NET Upgrades & Framework Support
All six plug-in shells (versions 2022 to 2027) target `.NET Framework 4.8` (`net48`). This is an intentional choice because Navisworks Manage desktop has not transitioned its main in-process add-in API to .NET Core/8.0 (unlike Revit or AutoCAD).

> [!IMPORTANT]
> **TFM Upgrade Directive:**
> If a future version of Autodesk Navisworks Manage moves to .NET Core/8.0+, add a `net8.0-windows` (or appropriate) target framework TFM for that specific version's plug-in project shell. Until such a change is announced, all current and legacy plug-ins must remain on `net48`.
