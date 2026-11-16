# Visibility of ventilation schemes

The ribbon command `cmdCreate3DScheme` passes the actual system class to the
generator. Only `MechanicalSystem` invokes `VentilationSchemeVisibility`; a
`PipingSystem` retains the existing behavior. Selection is independent of the
system's name, language and chosen filter parameter.

After the existing system filter is configured, the ventilation view hides:

- Revit links and coordination models;
- linked CAD file categories (embedded imports are not selected by this rule);
- Generic Models;
- pipes, flexible pipes, placeholders, fittings, accessories, insulation,
  segments, piping systems and fabrication pipework;
- Plumbing Equipment, Plumbing Fixtures and Sprinklers.

`OST_MechanicalEquipment` (Russian category «Оборудование») is explicitly enabled.
The existing system filter continues to select the relevant equipment. It is
different from `OST_PlumbingEquipment` («Сантехническое оборудование»).

The rule applies to both newly created views and existing scheme views reused by
name. Template views are excluded from that lookup. It uses category visibility,
not temporary isolation or link unloading. `CanAddSubcategory` does not determine
whether a category can be hidden.

Compile the production visibility helper against the installed Revit 2023 API:

```powershell
dotnet build tests/VentilationSchemes/ApiProbe.csproj -c Release --configfile tests/BatchParams.Values/NuGet.Config
```

Also build the full plugin for net48 and net8.0-windows. These are compile checks;
no model was opened and no visibility change was exercised in Revit for this task.
Runtime acceptance: create and repeat a ventilation scheme containing pipes,
Generic Models, mechanical equipment and links, check category visibility, then
create a piping scheme and confirm its original category behavior.

API reference: [Autodesk View Graphics](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Basic_Interaction_with_Revit_Elements/Views/Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Views_View_Graphics_html.html).
