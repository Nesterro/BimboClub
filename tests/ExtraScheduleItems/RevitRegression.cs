// Run this body with rvt-mcp send_code_to_revit in Revit 2024.
// Uses a disposable project, never the active/user model.
var assembly = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(
    @"D:\Revit\Plugins\BimboClub\SystemTravers\bin\x64\Release\net48\BimboClub.dll"));
var service = assembly.GetType("BimboClub.ExtraScheduleItems.ExtraScheduleService");
var itemType = assembly.GetType("BimboClub.ExtraScheduleItems.ExtraScheduleItem");
var project = app.Application.NewProjectDocument(UnitSystem.Metric);
var codeGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");
var makerGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");
var oldCodeGuid = new Guid("a22efc36-7c9b-4e08-9dfc-91aa0350d755");
var oldMakerGuid = new Guid("aa041857-96a2-4a0b-8526-cb17a5be5573");
var checks = new List<string>();
Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); checks.Add(name); };
Func<object, string, object> get = (item, name) => itemType.GetProperty(name).GetValue(item);
Action<object, string, object> set = (item, name, value) => itemType.GetProperty(name).SetValue(item, value);
Func<System.Collections.IList> scan = () => (System.Collections.IList)service.GetMethod("ScanExistingItems").Invoke(null, new object[] { project });
Action<System.Collections.IList> sync = items => service.GetMethod("SyncWithModel").Invoke(null, new object[] { project, items, new List<int>() });
Func<object, Element> element = item => project.GetElement(new ElementId((int)get(item, "Id")));
string originalShared = app.Application.SharedParametersFilename;
try
{
    var rows = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
    foreach (string code in new[] { "001-А", "002-Б" })
    {
        var item = Activator.CreateInstance(itemType);
        set(item, "Name", "ADSK regression " + code);
        set(item, "Code", code);
        set(item, "Manufacturer", "Завод " + code);
        rows.Add(item);
    }
    sync(rows);
    check(rows.Cast<object>().All(i => element(i).get_Parameter(codeGuid).AsString() == (string)get(i, "Code") &&
        element(i).get_Parameter(makerGuid).AsString() == (string)get(i, "Manufacturer")), "New rows: canonical GUIDs, independent values");
    check(scan().Count == 2, "Reopen: two rows retained");

    // Recreate the pre-2.4.24 family: wrong GUIDs, one parameter stored on the type.
    var family = ((FamilyInstance)element(rows[0])).Symbol.Family;
    var familyDoc = project.EditFamily(family);
    try
    {
        app.Application.SharedParametersFilename = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimboClub", "Families", "BCC_ADSK_SharedParams.txt");
        var group = app.Application.OpenSharedParameterFile().Groups.Create("Regression_" + Guid.NewGuid().ToString("N"));
        using (var tx = new Transaction(familyDoc, "Legacy parameter fixture"))
        {
            tx.Start();
            var manager = familyDoc.FamilyManager;
            foreach (var pair in new[] { Tuple.Create("ADSK_Код изделия", codeGuid, oldCodeGuid), Tuple.Create("ADSK_Завод-изготовитель", makerGuid, oldMakerGuid) })
            {
                var definition = (ExternalDefinition)group.Definitions.Create(new ExternalDefinitionCreationOptions(pair.Item1, SpecTypeId.String.Text) { GUID = pair.Item3 });
                var temp = manager.ReplaceParameter(manager.get_Parameter(pair.Item2), "Old_" + Guid.NewGuid().ToString("N"), GroupTypeId.Data, true);
                manager.ReplaceParameter(temp, definition, GroupTypeId.Data, pair.Item2 != makerGuid);
            }
            tx.Commit();
        }
        var loadOptions = (IFamilyLoadOptions)Activator.CreateInstance(service.GetNestedType("ExtraScheduleFamilyLoadOptions"));
        familyDoc.LoadFamily(project, loadOptions);
    }
    finally { familyDoc.Close(false); }
    using (var tx = new Transaction(project, "Legacy values"))
    {
        tx.Start();
        foreach (var row in rows)
            element(row).get_Parameter(oldCodeGuid).Set((string)get(row, "Code"));
        ((FamilyInstance)element(rows[0])).Symbol.get_Parameter(oldMakerGuid).Set("Старый завод");
        tx.Commit();
    }
    rows = scan();
    check(rows.Cast<object>().All(i => (string)get(i, "Manufacturer") == "Старый завод"), "Legacy type values read before migration");
    sync(rows);
    check(rows.Cast<object>().All(i => element(i).get_Parameter(codeGuid).AsString() == (string)get(i, "Code") &&
        element(i).get_Parameter(makerGuid).AsString() == "Старый завод"), "Loaded legacy family migrated with instance values retained");

    // A schedule uses parameter IDs/GUIDs, not the display name.
    using (var tx = new Transaction(project, "Canonical schedule"))
    {
        tx.Start();
        var schedule = ViewSchedule.CreateSchedule(project, new ElementId(BuiltInCategory.OST_GenericModel));
        schedule.Definition.IsItemized = true;
        schedule.Definition.AddField(ScheduleFieldType.Instance, SharedParameterElement.Lookup(project, codeGuid).Id);
        schedule.Definition.AddField(ScheduleFieldType.Instance, SharedParameterElement.Lookup(project, makerGuid).Id);
        project.Regenerate();
        var body = schedule.GetTableData().GetSectionData(SectionType.Body);
        var cells = new List<string>();
        for (int r = body.FirstRowNumber; r <= body.LastRowNumber; r++)
            for (int c = body.FirstColumnNumber; c <= body.LastColumnNumber; c++)
                cells.Add(schedule.GetCellText(SectionType.Body, r, c));
        check(cells.Contains("001-А") && cells.Contains("002-Б") && cells.Contains("Старый завод"), "Schedule cells show both product codes and manufacturer");
        tx.Commit();
    }

    set(rows[0], "Code", "");
    set(rows[0], "Manufacturer", "");
    set(rows[1], "Manufacturer", "Другой завод");
    sync(rows);
    rows = scan();
    check(rows.Cast<object>().Count(i => (string)get(i, "Code") == "" && (string)get(i, "Manufacturer") == "") == 1 &&
        rows.Cast<object>().Count(i => (string)get(i, "Manufacturer") == "Другой завод") == 1, "Clear/update/reopen: values remain independent");

    // Repeat synchronization of an already migrated family.
    sync(rows);
    check(scan().Count == 2, "Repeated synchronization is idempotent");

    // A project can retain a second, same-name parameter with the legacy GUID.
    using (var tx = new Transaction(project, "Duplicate parameter fixture"))
    {
        tx.Start();
        var oldDefinition = SharedParameterElement.Lookup(project, oldCodeGuid).GetDefinition();
        var categories = app.Application.Create.NewCategorySet();
        categories.Insert(project.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel));
        project.ParameterBindings.Insert(oldDefinition, app.Application.Create.NewInstanceBinding(categories), GroupTypeId.Data);
        tx.Commit();
    }
    set(rows[0], "Code", "009-duplicate");
    sync(rows);
    check(element(rows[0]).GetParameters("ADSK_Код изделия").Count == 2 &&
        element(rows[0]).GetParameters("ADSK_Код изделия").All(p => p.AsString() == "009-duplicate"), "Duplicate-name parameters both updated");

    // Reject read-only fields and keep model/UI state intact after rollback.
    familyDoc = project.EditFamily(((FamilyInstance)element(rows[0])).Symbol.Family);
    try
    {
        using (var tx = new Transaction(familyDoc, "Read-only parameter fixture"))
        {
            tx.Start();
            familyDoc.FamilyManager.SetFormula(familyDoc.FamilyManager.get_Parameter(codeGuid), "\"locked\"");
            tx.Commit();
        }
        familyDoc.LoadFamily(project, (IFamilyLoadOptions)Activator.CreateInstance(service.GetNestedType("ExtraScheduleFamilyLoadOptions")));
    }
    finally { familyDoc.Close(false); }
    var newRow = Activator.CreateInstance(itemType);
    set(newRow, "Code", "must-fail");
    set(newRow, "IsNew", true);
    var failureRows = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
    failureRows.Add(newRow);
    bool rejected = false;
    try { sync(failureRows); }
    catch (System.Reflection.TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
    check(rejected && (int)get(newRow, "Id") == 0 && (bool)get(newRow, "IsNew") && scan().Count == 2,
        "Read-only failure rolls back new elements and keeps row unsaved");
    return new { passed = checks.Count, checks };
}
catch (Exception ex) { return new { passed = checks.Count, checks, error = ex.ToString() }; }
finally
{
    app.Application.SharedParametersFilename = originalShared;
    project.Close(false);
}
