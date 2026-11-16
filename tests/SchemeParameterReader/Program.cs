using System;
using System.Linq;
using Autodesk.Revit.DB;
using BimboClub;

internal static class Program
{
    private static int count;
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        count++;
    }
    private static void Main()
    {
        var doc = new Document();
        var builtIn = BuiltInParameter.RBS_SYSTEM_NAME_PARAM;
        var reader = new SchemeParameterReader(doc, new ElementId((int)builtIn));
        var unit = new Element { Id = new ElementId(101) };
        var system = new Parameter { Value = "В7.3,П7.3,В7.3" };
        unit.BuiltIns[builtIn] = system;
        Check(reader.Read(unit) == system.Value, "Reads full built-in multi-system value directly");
        Check(system.Reads == 1, "Reads through Parameter.AsString");
        var missing = new Element { Id = new ElementId(102) };
        Check(reader.Read(missing) == null, "Missing parameter is a normal absence");
        Check(reader.Read(null) == null, "Null input is ignored");
        var numeric = new Parameter { StorageType = StorageType.Integer, Value = "123" };
        missing.BuiltIns[builtIn] = numeric;
        Check(reader.Read(missing) == null && numeric.Reads == 0, "Never calls AsString for numeric storage");
        var unset = new Parameter { HasValue = false };
        missing.BuiltIns[builtIn] = unset;
        Check(reader.Read(missing) == null && unset.Reads == 0, "Never reads an unset parameter");
        var type = new Element { Id = new ElementId(200) };
        type.BuiltIns[builtIn] = new Parameter { Value = "В7.3" };
        doc.Elements[type.Id] = type;
        missing.TypeId = type.Id;
        Check(reader.Read(missing) == "В7.3", "Falls back to type when instance has no value");
        missing.BuiltIns.Clear();
        Check(reader.Read(missing) == "В7.3", "Falls back to type when instance lacks parameter");
        missing.BuiltIns[builtIn] = new Parameter { Value = "" };
        Check(reader.Read(missing) == "", "Explicit empty instance value is preserved");
        missing.BuiltIns[builtIn] = new Parameter { Value = "П9" };
        Check(reader.Read(missing) == "П9", "Instance value has priority over type");
        var definition = new Definition { Name = "Имя системы" };
        var wrongSameName = new Definition { Name = "Имя системы" };
        var parameterElement = new ParameterElement { Id = new ElementId(500), Definition = definition };
        doc.Elements[parameterElement.Id] = parameterElement;
        var custom = new SchemeParameterReader(doc, parameterElement.Id);
        unit.Defined[definition] = new Parameter { Value = "П7.3,В7.3" };
        unit.Defined[wrongSameName] = new Parameter { Value = "НЕВЕРНО" };
        Check(custom.Read(unit) == "П7.3,В7.3", "Uses selected project/shared definition, not a duplicate display name");
        type.Defined[definition] = new Parameter { Value = "В7.3,П8" };
        Check(custom.Read(missing) == "В7.3,П8", "Selected definition also supports type parameters");
        var values = new[] { unit, missing, new Element { Id = new ElementId(103) } }.Select(reader.Read).ToList();
        var matching = SchemeSystemMembership.GetMatchingValues("В7.3", values);
        Check(matching.Contains(system.Value) && !matching.Contains("П9"), "Reader output retains shared AHU and excludes unrelated equipment");
        system.Fail = true;
        bool informative = false;
        try { reader.Read(unit); }
        catch (InvalidOperationException ex) { informative = ex.Message.Contains("101") && ex.InnerException != null; }
        Check(informative, "Real read failures include element ID and are not silently dropped");
        bool invalid = false;
        try { new SchemeParameterReader(doc, new ElementId(999)); }
        catch (ArgumentException) { invalid = true; }
        Check(invalid, "Unknown selected definition fails explicitly");
        Console.WriteLine($"PASS: {count} reader checks using production reader/matcher and an API test double; no Revit/model loaded.");
    }
}
