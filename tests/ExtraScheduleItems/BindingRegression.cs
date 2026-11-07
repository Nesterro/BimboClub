// Run with revit_send_code_to_revit. Change assemblyPath to test another build.
// Each case owns a temporary document; the active model is never modified.
string assemblyPath = @"D:\Revit\Plugins\BimboClub\SystemTravers\bin\x64\Release\net48\BimboClub.dll";
var assembly = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(assemblyPath));
var service = assembly.GetType("BimboClub.ExtraScheduleItems.ExtraScheduleService");
var itemType = assembly.GetType("BimboClub.ExtraScheduleItems.ExtraScheduleItem");
var makerGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");
string originalShared = app.Application.SharedParametersFilename;
var results = new List<object>();
try
{
    app.Application.SharedParametersFilename = (string)service.GetMethod("EnsureAdskSharedParamFile",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
    var definition = app.Application.OpenSharedParameterFile().Groups.get_Item("ADSK").Definitions.get_Item("ADSK_Завод-изготовитель");
    foreach (string bindingKind in new[] { "None", "Instance", "Type" })
    {
        var project = app.Application.NewProjectDocument(UnitSystem.Metric);
        try
        {
            if (bindingKind != "None")
            {
                using (var tx = new Transaction(project, "Fixture: project manufacturer binding"))
                {
                    tx.Start();
                    var categories = app.Application.Create.NewCategorySet();
                    categories.Insert(project.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel));
                    Binding binding = bindingKind == "Type"
                        ? (Binding)app.Application.Create.NewTypeBinding(categories)
                        : app.Application.Create.NewInstanceBinding(categories);
                    if (!project.ParameterBindings.Insert(definition, binding, BuiltInParameterGroup.PG_DATA))
                        throw new Exception("Fixture binding insertion failed");
                    tx.Commit();
                }
            }
            var rows = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            for (int i = 0; i < 19; i++)
            {
                var item = Activator.CreateInstance(itemType);
                itemType.GetProperty("Name").SetValue(item, "Regression row " + i);
                itemType.GetProperty("Manufacturer").SetValue(item, i % 3 == 0 ? "" : "Завод " + i);
                rows.Add(item);
            }
            service.GetMethod("SyncWithModel").Invoke(null, new object[] { project, rows, new List<int>() });
            var actual = rows.Cast<object>().Select(row => new {
                expected = (string)itemType.GetProperty("Manufacturer").GetValue(row),
                actual = project.GetElement(new ElementId((int)itemType.GetProperty("Id").GetValue(row))).get_Parameter(makerGuid)?.AsString() ?? ""
            }).ToList();
            results.Add(new { bindingKind, passed = actual.All(r => r.expected == r.actual), actual });
        }
        catch (Exception ex) { results.Add(new { bindingKind, passed = false, error = ex.ToString() }); }
        finally { project.Close(false); }
    }
    return new { revit = app.Application.VersionNumber, build = app.Application.VersionBuild, results };
}
finally { app.Application.SharedParametersFilename = originalShared; }
