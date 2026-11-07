// Run with revit_send_code_to_revit in Revit 2023 or later.
// Change assemblyPath to compare the original and fixed builds.
string assemblyPath = @"D:\Revit\Plugins\BimboClub\SystemTravers\bin\x64\Release\net48\BimboClub.dll";
var assembly = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(assemblyPath));
var service = assembly.GetType("BimboClub.ExtraScheduleItems.ExtraScheduleService");
var itemType = assembly.GetType("BimboClub.ExtraScheduleItems.ExtraScheduleItem");
var project = app.Application.NewProjectDocument(UnitSystem.Metric);
var codeGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");
var makerGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");
var checks = new List<string>();
Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); checks.Add(name); };
Func<object, string, object> get = (row, name) => itemType.GetProperty(name).GetValue(row);
Action<object, string, object> set = (row, name, value) => itemType.GetProperty(name).SetValue(row, value);
Func<System.Collections.IList> newRows = () => (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
Func<System.Collections.IList> scan = () => (System.Collections.IList)service.GetMethod("ScanExistingItems").Invoke(null, new object[] { project });
Action<System.Collections.IList> sync = rows => service.GetMethod("SyncWithModel").Invoke(null, new object[] { project, rows, new List<int>() });
Func<object, Element> element = row => project.GetElement(new ElementId((int)get(row, "Id")));
string originalShared = app.Application.SharedParametersFilename;
try
{
    string[] makers = { "Hisense ", " Hisense", " Fachmann ", "\tЗавод\t", "K-FLEX\u00a0",
        "  ", "\r\n", "\t", "\u00a0", "", "Завод\r\nФилиал", "Завод\nФилиал", "Завод\rФилиал",
        " a  b ", "a\tb", "К-ФЛЕКС", "DKS", "\u200b", "Завод \u00a0 филиал" };
    var rows = newRows();
    foreach (string maker in makers)
    {
        var row = Activator.CreateInstance(itemType);
        set(row, "Name", "Whitespace regression " + rows.Count);
        set(row, "Manufacturer", maker);
        set(row, "IsNew", true);
        rows.Add(row);
    }
    sync(rows);
    check(rows.Cast<object>().Select((row, i) => (element(row).get_Parameter(makerGuid).AsString() ?? "") == makers[i].Trim()).All(ok => ok),
        "19 new rows: trailing/leading spaces, tabs, NBSP, whitespace-only, empty and internal whitespace");
    check(rows.Cast<object>().Select((row, i) => (string)get(row, "Manufacturer") == makers[i].Trim() && !(bool)get(row, "IsNew") && !(bool)get(row, "IsModified")).All(ok => ok),
        "UI text and flags match stored values after success");
    check(scan().Cast<object>().Select(row => (string)get(row, "Manufacturer")).OrderBy(v => v).SequenceEqual(makers.Select(v => v.Trim()).OrderBy(v => v)),
        "Reopen preserves all normalized manufacturer values");

    for (int i = 0; i < rows.Count; i++) set(rows[i], "Code", " \t00" + i + "-А\u00a0 ");
    sync(rows);
    check(rows.Cast<object>().Select((row, i) => element(row).get_Parameter(codeGuid).AsString() == "00" + i + "-А" && (string)get(row, "Code") == "00" + i + "-А").All(ok => ok),
        "Update product codes: edge whitespace removed, leading zeros and Cyrillic preserved");
    sync(rows);
    check(scan().Count == 19, "Repeated sync creates no duplicates");

    using (var tx = new Transaction(project, "Verify actual schedule cells"))
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
        check(cells.Contains("000-А") && cells.Contains("0018-А") && cells.Contains("Hisense") && cells.Contains("Fachmann"),
            "Actual schedule contains product codes and normalized manufacturers");
        tx.Commit();
    }

    // A NUL truncates Revit text. Unlike edge whitespace this must remain a failure.
    string savedMaker = element(rows[0]).get_Parameter(makerGuid).AsString();
    set(rows[0], "Manufacturer", " Changed ");
    var invalidRow = Activator.CreateInstance(itemType);
    set(invalidRow, "Manufacturer", "Завод\0LOST");
    set(invalidRow, "IsNew", true);
    var failureRows = newRows();
    failureRows.Add(rows[0]);
    failureRows.Add(invalidRow);
    string rejected = null;
    try { sync(failureRows); }
    catch (System.Reflection.TargetInvocationException ex) { if (ex.InnerException is InvalidOperationException) rejected = ex.InnerException.Message; else throw; }
    check(rejected != null && rejected.Contains("ADSK_Завод-изготовитель"), "Real truncation still rejects synchronization");
    check(element(rows[0]).get_Parameter(makerGuid).AsString() == savedMaker && scan().Count == 19,
        "Failure rolls back both existing-row changes and newly created elements");
    check((string)get(rows[0], "Manufacturer") == " Changed " && (bool)get(rows[0], "IsModified") &&
        (int)get(invalidRow, "Id") == 0 && (bool)get(invalidRow, "IsNew") && (string)get(invalidRow, "Manufacturer") == "Завод\0LOST",
        "Failure preserves unsaved UI text, IDs and flags");
    check(rejected.Contains("Ожидалось:") && rejected.Contains("прочитано:") && rejected.Contains("\\0"),
        "Mismatch diagnostic shows expected/actual text and escaped NUL");
    return new { revit = app.Application.VersionNumber, build = app.Application.VersionBuild, passed = checks.Count, checks };
}
catch (Exception ex) { return new { passed = checks.Count, checks, error = ex.ToString() }; }
finally { app.Application.SharedParametersFilename = originalShared; project.Close(false); }
