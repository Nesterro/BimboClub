using System;
using System.Linq;
using Autodesk.Revit.DB;
using BimboClub;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        checks++;
    }
    private static void Fails(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { checks++; return; }
        throw new Exception(message);
    }
    private static FamilyParameter Param(StorageType type, string spec = "text") => new FamilyParameter
    { StorageType = type, Definition = new Definition { Name = "Test", DataType = new ForgeTypeId(spec) } };
    private static void Main()
    {
        var doc = new Document();
        var manager = doc.FamilyManager;
        var text = Param(StorageType.String);
        // Regression: both old formula and fallback Set failed with no current type.
        Fails(() => manager.SetFormula(text, "\"Test\""), "The old code needs a current type");
        Fails(() => manager.Set(text, "Test"), "The old fallback also needs a current type");
        var verify = BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "Насос" });
        verify();
        Check(manager.Types.Count == 1 && manager.CurrentType.AsString(text) == "Насос", "Creates a type and writes the value");
        var first = manager.CurrentType;
        var second = manager.NewType("Второй");
        manager.CurrentType = first;
        text.Formula = "Old formula";
        BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "  ООО \"Завод\" 01  A  ", DataType = "WRONG_HINT" })();
        Check(manager.Types.All(t => t.AsString(text) == "ООО \"Завод\" 01  A"), "Literal fills every type and preserves internal spaces/quotes");
        Check(text.Formula == null && manager.CurrentType == first, "Removes previous formula and restores current type");
        BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "\"\"" })();
        Check(manager.Types.All(t => t.AsString(text) == ""), "Quoted empty string clears text");
        Check(BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = " " }) == null, "Blank cell means unchanged");
        var empty = new Document();
        Check(BatchFamilyValueWriter.Apply(empty, text, new SharedParamItem()) == null && empty.FamilyManager.Types.Count == 0, "No type created if nothing to fill");

        foreach (var input in new[] { "0", "-2", "123" })
        {
            var parameter = Param(StorageType.Integer, "integer");
            BatchFamilyValueWriter.Apply(doc, parameter, new SharedParamItem { DefaultValue = input })();
            Check(manager.Types.All(t => t.AsInteger(parameter) == int.Parse(input)), "Integer overload including zero");
        }
        foreach (var pair in new[] { ("Да", 1), ("Нет", 0), ("true", 1), ("FALSE", 0), ("0", 0), ("1", 1) })
        {
            var parameter = Param(StorageType.Integer, "yesno");
            BatchFamilyValueWriter.Apply(doc, parameter, new SharedParamItem { DefaultValue = pair.Item1 })();
            Check(manager.Types.All(t => t.AsInteger(parameter) == pair.Item2), "Yes/no conversion");
        }
        Fails(() => BatchFamilyValueWriter.Apply(doc, Param(StorageType.Integer, "yesno"), new SharedParamItem { DefaultValue = "2" }), "Invalid yes/no must not succeed");
        Fails(() => BatchFamilyValueWriter.Apply(doc, Param(StorageType.Integer), new SharedParamItem { DefaultValue = "2.5" }), "Fraction cannot silently become an integer");
        foreach (var input in new[] { "1.5", "1,5", "0" })
        {
            var parameter = Param(StorageType.Double, "number");
            BatchFamilyValueWriter.Apply(doc, parameter, new SharedParamItem { DefaultValue = input })();
            Check(manager.Types.All(t => t.AsDouble(parameter) == (input == "0" ? 0 : 1.5)), "Double parsing uses displayed units");
        }
        var length = Param(StorageType.Double, "length");
        BatchFamilyValueWriter.Apply(doc, length, new SharedParamItem { DefaultValue = "304.8 мм" })();
        Check(first.AsDouble(length) == 1, "Units passed to Revit parser, not interpreted as internal feet");

        var preset = new SharedParamItem { DefaultValue = "stale", Formula = "Width / 2" };
        Check(preset.ValueOrFormula == "=Width / 2", "Preset formula is visible in the edited cell");
        var presetPath = System.IO.Path.GetTempFileName();
        try
        {
            BatchParamsPreset.SavePreset(presetPath, new BatchParamsPreset { Items = new System.Collections.Generic.List<SharedParamItem> { preset } });
            Check(BatchParamsPreset.LoadPreset(presetPath).Items[0].ValueOrFormula == "=Width / 2", "JSON retains explicit formula");
            BatchParamsPreset.ExportToCsv(presetPath, new System.Collections.Generic.List<SharedParamItem> { preset });
            Check(BatchParamsPreset.ImportFromCsv(presetPath)[0].ValueOrFormula == "=Width / 2", "CSV retains explicit formula marker");
        }
        finally { System.IO.File.Delete(presetPath); }
        preset.ValueOrFormula = "новое значение";
        Check(preset.Formula == "" && preset.DefaultValue == "новое значение", "Cell edit replaces hidden preset formula");
        BatchFamilyValueWriter.Apply(doc, text, preset)();
        Check(first.AsString(text) == "новое значение", "Visible cell is what is written");
        BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "=OtherText" })();
        Check(text.Formula == "OtherText", "Explicit text formula remains a formula, not quoted text");
        BatchFamilyValueWriter.Apply(doc, length, new SharedParamItem { DefaultValue = "Width / 2" })();
        Check(length.Formula == "Width / 2", "Legacy numeric expressions remain supported");
        BatchFamilyValueWriter.Apply(doc, length, new SharedParamItem { Formula = "if(Width > 1, Width, 1)" })();
        Check(length.Formula.Contains(","), "Do not corrupt argument separators");
        Fails(() => BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "=Broken(" }), "Invalid formula must propagate");
        Fails(() => BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "=" }), "Empty formula must fail");
        text.Formula = null;
        manager.IgnoreFormula = true;
        verify = BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { Formula = "OtherText" });
        Fails(verify, "Missing formula must fail verification");
        manager.IgnoreFormula = false;
        text.ReadOnly = true;
        Fails(() => BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "Cannot write" }), "Read-only must propagate");
        text.ReadOnly = false;
        manager.IgnoreWrites = true;
        verify = BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "Different value" });
        Fails(verify, "A silent no-op must fail verification");
        manager.IgnoreWrites = false;
        manager.TruncateText = true;
        verify = BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "Long value" });
        Fails(verify, "Truncation must fail verification");
        manager.TruncateText = false;
        manager.FailOnType = second;
        Fails(() => BatchFamilyValueWriter.Apply(doc, text, new SharedParamItem { DefaultValue = "Cannot fill second" }), "Later type failure must propagate for batch rollback");
        Check(manager.CurrentType == first, "Current type restored after failure");
        Console.WriteLine($"PASS: {checks} checks using production writer/models and a Revit API test double. No models opened.");
    }
}
