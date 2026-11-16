# Multi-system equipment in schemes

The previous inverse filter hid every parameter value not equal to the single
selected system name. Therefore `В7.3,П7.3,В7.3` was hidden on both schemes.

`SchemeSystemMembership` matches full tokens, ignoring surrounding whitespace and
case. During generation, the generator reads the chosen parameter from the
filterable MEP categories, collects distinct matching full values and builds an
AND of native Revit `NotEquals` rules. The inverse filter hides an element only
when it differs from every accepted value. Existing named filters are updated.
This does not use substring matching or override visibility with an unrelated
"show" filter (which would not cancel the old hide filter).

The rules represent combinations present at generation time. Rerun generation
after changing a unit's system membership, using the same view prefix/suffix.
Other user-applied view filters/templates or individually hidden elements are
not removed. No view/model was accessed during these offline checks.

```powershell
dotnet restore tests/SchemeMembership/SchemeMembership.csproj --configfile tests/BatchParams.Values/NuGet.Config
dotnet run --project tests/SchemeMembership/SchemeMembership.csproj -c Release -f net48 --no-restore
dotnet run --project tests/SchemeMembership/SchemeMembership.csproj -c Release -f net8.0 --no-restore
dotnet build tests/VentilationSchemes/ApiProbe.csproj -c Release --configfile tests/BatchParams.Values/NuGet.Config
```

29 checks per runtime cover the reported repeated list, both member systems,
prefix/suffix collisions, spaces/NBSP/tabs, empty values, preservation of full
strings for native rules, the AND-of-inequalities truth table and refreshed lists.
The compile probe links the production generator, visibility helper and matcher
against Revit 2023 API; only the WPF data contract is represented separately.
Offline tests do not execute Revit's filter or category visibility APIs.

API reference: [Autodesk GetElementFilter](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/9b14bfc0-597d-36ab-77d6-d801c3053873.htm)
documents conjunctions of filter rules.
