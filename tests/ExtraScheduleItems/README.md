# ExtraScheduleItems regression

`RevitRegression.cs` is a C# body for rvt-mcp `revit_send_code_to_revit`, not a standalone executable. Build the Release/net48 plugin, adjust the DLL path if necessary, open an empty Revit 2024 project, then execute the body. It creates and closes its own temporary project and restores the application's shared-parameter path. It also exercises the plugin's normal family cache in AppData.

Verified on Revit 2024.1 for release 2.4.24: all nine checks passed (new rows, reading, legacy GUID/type migration, actual schedule cells, clearing, repeated sync, duplicate names, rollback/UI IDs on a read-only field). Both net48 and net8.0-windows builds passed. Other Revit versions were not exercised interactively.

The canonical product-code and manufacturer GUIDs are independently listed in the manufacturer's [OSTEC Revit guide, appendix 2](https://ostec.ru/upload/iblock/46a/5gsa1ytfjv7gee57eckos567v09bqnts/Rukovodstvo-polzovatelya.pdf). Revit identifies shared parameters by GUID rather than display name.
