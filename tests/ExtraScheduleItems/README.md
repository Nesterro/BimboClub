# ExtraScheduleItems regression

`RevitRegression.cs` is a C# body for rvt-mcp `revit_send_code_to_revit`, not a standalone executable. Build the Release/net48 plugin, adjust the DLL path if necessary, open an empty Revit 2024 project, then execute the body. It creates and closes its own temporary project and restores the application's shared-parameter path. It also exercises the plugin's normal family cache in AppData.

Verified on Revit 2024.1 for release 2.4.24: all nine checks passed (new rows, reading, legacy GUID/type migration, actual schedule cells, clearing, repeated sync, duplicate names, rollback/UI IDs on a read-only field). Both net48 and net8.0-windows builds passed. Other Revit versions were not exercised interactively.

The canonical product-code and manufacturer GUIDs are independently listed in the manufacturer's [OSTEC Revit guide, appendix 2](https://ostec.ru/upload/iblock/46a/5gsa1ytfjv7gee57eckos567v09bqnts/Rukovodstvo-polzovatelya.pdf). Revit identifies shared parameters by GUID rather than display name.

## 2.4.25: text normalization (2026-11-07)

Reproduced in Revit 2023.1, build 23.1.10.4: `Parameter.Set("Hisense ")`
stores `"Hisense"`. The original service compared that result with the untrimmed
input and rolled back all rows with "Не удалось сохранить ADSK_Завод-изготовитель".
Tabs and NBSP at the edges and whitespace-only values have the same problem.
The fix trims product code/manufacturer before writing and comparison; it updates
the UI row only after the transaction group succeeds. Internal whitespace, case
and leading zeros remain significant. Actual truncation must still fail.

Run `TextRegression.cs` and `BindingRegression.cs` through rvt-mcp in addition to
`RevitRegression.cs`, adjusting the assembly path in each body. The text suite
creates 19 rows, checks update/reopen/repeat and actual schedule cells, then uses
an embedded NUL to verify real truncation, atomic rollback and unsaved UI state.
The binding suite tests no project binding, instance binding and type binding.
The original build fails the first text case; the fixed full DLL passed 10 text,
9 existing and 3 binding checks in Revit 2023. This is a synthetic reproduction;
the reporting user's project was unavailable.

`dotnet build tests/ExtraScheduleItems/ServiceProbe.csproj -c Release` also compiles
the production service/item/logger against the locally installed Revit 2023 API.
Override `-p:RevitYear=2024` for another installed API. This probe is only a test
assembly, not an add-in to install. Full net48 and net8 plugin builds are required
for release. A successful build alone does not exercise other Revit versions.

These integration tests use `%APPDATA%/BimboClub/Families`. Use a disposable test
profile, or back up and restore its files. The current cache is shared across
Revit years: a cached RFA saved by a newer Revit cannot be loaded by Revit 2023.
For a clean test fixture generate the cache with the tested Revit year. This
separate cache limitation is not changed by the text fix.
