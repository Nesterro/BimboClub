# Opening dimensions and bottom elevation rounding

The tests link production `OpeningRoundingOptions` and `OpeningRoundingService`
against an in-memory Revit API double. 75 assertions per runtime cover parsing,
upward size rounding, downward negative elevations, conversion tolerance,
actual geometry with bottom/center/top family origins, nested geometry,
preserving vertical extent, idempotence, disabled rounding, missing geometry,
readonly/rejected parameters and constrained placement.

```powershell
dotnet restore tests/OpeningRounding/OpeningRounding.csproj --configfile tests/BatchParams.Values/NuGet.Config
dotnet run --project tests/OpeningRounding/OpeningRounding.csproj -c Release -f net48 --no-restore
dotnet run --project tests/OpeningRounding/OpeningRounding.csproj -c Release -f net8.0 --no-restore
dotnet build tests/OpeningRounding.Api2023/ApiProbe.csproj -c Release --configfile tests/BatchParams.Values/NuGet.Config
```

The API probe compiles the production service against installed Revit 2023 API.
The full plugin build also checks all placement/merge command and WPF integration.
No real model was used; test doubles do not establish Revit geometry/transaction
behavior. Runtime acceptance must check wall/floor families, nonzero and negative
level-relative bottom values, manual/batch merging, locked placement/parameters,
and preservation of source openings on failure.

The bottom comes from solid/mesh geometry, not an element bounding box (which can
include family flip controls). Level.ProjectElevation uses the same internal
coordinate frame as the geometric Z value; no assumption about shared elevation
display settings is made. Reference: [Autodesk ProjectElevation](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/895ef506-bfea-cc4e-31f8-aad2af6672e4.htm).

If a family has no measurable solid/mesh geometry, disable bottom rounding (0).
All supplied dimension parameters must be writable instance length parameters.
Geometry is regenerated and checked before sources are deleted in merging.
Placement uses a subtransaction per opening so failed rounding leaves no partial
new instance. In manual/batch merging, failures roll back the enclosing transaction.
