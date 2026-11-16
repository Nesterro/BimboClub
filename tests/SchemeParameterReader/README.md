# Direct parameter reading for scheme filters

The reported 2.4.30 stack fails inside
`Autodesk.Revit.DB.FilterableValueProvider.GetStringValue`, called from the lazy
enumeration consumed by `SchemeSystemMembership.GetMatchingValues`.

The generator now regenerates before collecting MEP values and uses the production
`SchemeParameterReader`, then materializes the values before matching. The reader
resolves built-in IDs or a `ParameterElement` definition, checks `StorageType.String`
and `HasValue`, and calls `Parameter.AsString`. It falls back to the type only for
missing values, preserving explicit empty instance strings. Real errors include
the element/parameter IDs and propagate to the command's transaction rollback.

15 offline assertions per runtime exercise the production reader and matcher with
an API test double: multi-system built-ins, missing/unset/numeric parameters,
instance/type precedence, duplicate display names, project/shared definitions,
the membership pipeline and errors. There is no `FilterableValueProvider` in the
test double, so a return to that API call would not compile. These tests do not
reproduce the native Revit internal error or establish runtime success in the
user's unavailable model.

```powershell
dotnet restore tests/SchemeParameterReader/SchemeParameterReader.csproj --configfile tests/BatchParams.Values/NuGet.Config
dotnet run --project tests/SchemeParameterReader/SchemeParameterReader.csproj -c Release -f net48 --no-restore
dotnet run --project tests/SchemeParameterReader/SchemeParameterReader.csproj -c Release -f net8.0 --no-restore
dotnet build tests/VentilationSchemes/ApiProbe.csproj -c Release --configfile tests/BatchParams.Values/NuGet.Config
```

The API probe compiles the full production generator and helpers against installed
Revit 2023 without opening Revit. Full plugin builds are also required for release.
Reference: [Autodesk parameters and direct access](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Basic_Interaction_with_Revit_Elements/Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_html.html).
