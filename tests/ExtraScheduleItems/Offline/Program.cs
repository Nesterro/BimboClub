using System;
using System.Collections.Generic;
using System.Linq;
using BimboClub.ExtraScheduleItems;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }
    private static void Reject(Action action, string field)
    {
        try { action(); }
        catch (InvalidOperationException ex)
        {
            Check(ex.Message.Contains(field), "Error must identify " + field);
            return;
        }
        throw new Exception("Accepted corrupt value: " + field);
    }

    private static void Main()
    {
        // Independent fixture transcribed from the published ADSK table, not generated
        // from the implementation. Ordering deliberately differs from the schema.
        var expectedGuids = new Dictionary<string, Guid>
        {
            ["ADSK_Наименование"] = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43"),
            ["ADSK_Марка"] = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d"),
            ["ADSK_Количество"] = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a"),
            ["ADSK_Масса"] = new Guid("32989501-0d17-4916-8777-da950841c6d7"),
            ["ADSK_Примечание"] = new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3"),
            ["ADSK_Единица измерения"] = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d"),
            ["ADSK_Позиция"] = new Guid("ae8ff999-1f22-4ed7-ad33-61503d85f0f4"),
            ["ADSK_Группирование"] = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401"),
            ["ADSK_Код изделия"] = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed"),
            ["ADSK_Завод-изготовитель"] = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351")
        };
        var fields = ExtraScheduleParameterSchema.Fields;
        Check(fields.Length == 10 && fields.Select(f => f.Guid).Distinct().Count() == 10, "Ten unique fields");
        foreach (var field in fields)
        {
            Check(field.Guid == expectedGuids[field.Name], "Official GUID: " + field.Name);
            Check(field.IsNumber == (field.Name == "ADSK_Количество" || field.Name == "ADSK_Масса"), "Storage type");
            foreach (var alias in field.Names)
                Check(ExtraScheduleParameterSchema.Find(new[] { alias }) == field, "Alias resolves canonical GUID");
        }

        var rows = Enumerable.Range(1, 19).Select(i => new ExtraScheduleItem
        {
            Id = i, IsNew = true, Group = " ОВ" + i + " ", Position = "00" + i,
            Name = "\tТруба  медная Ø" + i + "\u00a0", Mark = "M-" + i,
            Code = "00-" + i, Manufacturer = " Hisense " + i + " ", Unit = i % 2 == 0 ? "м" : "шт",
            Count = i + 0.5, Weight = i * 0.125, Note = "Примечание " + i
        }).ToArray();
        var stored = new Dictionary<int, Dictionary<Guid, object>>();
        foreach (var row in rows)
        {
            var values = stored[row.Id] = new Dictionary<Guid, object>();
            ExtraScheduleParameterSchema.Write(row, (field, value) => values.Add(field.Guid, value));
            Check(values.Count == 10, "Every field written");
            Check((string)values[expectedGuids["ADSK_Наименование"]] == "Труба  медная Ø" + row.Id, "Name mapping");
            Check((string)values[expectedGuids["ADSK_Марка"]] == "M-" + row.Id, "Mark mapping");
            Check((double)values[expectedGuids["ADSK_Количество"]] == row.Id + 0.5, "Count mapping");
            Check((double)values[expectedGuids["ADSK_Масса"]] == row.Id * 0.125, "Mass mapping");
            Check((string)values[expectedGuids["ADSK_Группирование"]] == "ОВ" + row.Id, "Group mapping");
            Check((string)values[expectedGuids["ADSK_Позиция"]] == "00" + row.Id, "Position mapping");
            Check((string)values[expectedGuids["ADSK_Код изделия"]] == "00-" + row.Id, "Code mapping");
            Check((string)values[expectedGuids["ADSK_Завод-изготовитель"]] == "Hisense " + row.Id, "Manufacturer mapping");
            Check((string)values[expectedGuids["ADSK_Единица измерения"]] == (row.Id % 2 == 0 ? "м" : "шт"), "Unit mapping");
            Check((string)values[expectedGuids["ADSK_Примечание"]] == "Примечание " + row.Id, "Note mapping");
        }
        foreach (var row in rows)
            ExtraScheduleParameterSchema.Verify(row, field => stored[row.Id][field.Guid]);
        Check(rows.All(r => r.IsNew && r.Name.StartsWith("\t") && r.Id > 0), "Write/verify do not prematurely mutate UI state");

        var first = rows[0];
        var firstValues = stored[first.Id];
        foreach (var field in fields)
        {
            object value = firstValues[field.Guid];
            firstValues[field.Guid] = field.IsNumber ? (object)999.0 : "corrupt";
            Reject(() => ExtraScheduleParameterSchema.Verify(first, f => firstValues[f.Guid]), field.Name);
            firstValues[field.Guid] = null;
            Reject(() => ExtraScheduleParameterSchema.Verify(first, f => firstValues[f.Guid]), field.Name);
            firstValues[field.Guid] = field.IsNumber ? (object)"1.5" : 123.0;
            Reject(() => ExtraScheduleParameterSchema.Verify(first, f => firstValues[f.Guid]), field.Name);
            firstValues[field.Guid] = value;
        }

        // All rows sharing a type-level mark must fail after the final row overwrites it.
        Guid mark = expectedGuids["ADSK_Марка"];
        foreach (var values in stored.Values) values[mark] = "M-19";
        Reject(() => ExtraScheduleParameterSchema.Verify(first, f => firstValues[f.Guid]), "ADSK_Марка");

        first.Group = first.Position = first.Name = first.Mark = first.Code = first.Manufacturer = first.Unit = first.Note = " \t\u00a0";
        first.Count = first.Weight = 0;
        ExtraScheduleParameterSchema.Write(first, (field, value) => firstValues[field.Guid] = value);
        ExtraScheduleParameterSchema.Verify(first, f => firstValues[f.Guid]);
        Check(firstValues.Values.All(v => v is string text ? text == "" : (double)v == 0), "Clear every field including numeric zero");

        first.Name = "Alpha  Beta";
        ExtraScheduleParameterSchema.Write(first, (field, value) => firstValues[field.Guid] = value);
        foreach (string corrupt in new[] { "Alpha Beta", "alpha  Beta", "Alpha", "Alpha  Beta " })
        {
            firstValues[expectedGuids["ADSK_Наименование"]] = corrupt;
            Reject(() => ExtraScheduleParameterSchema.Verify(first, f => firstValues[f.Guid]), "ADSK_Наименование");
        }
        var numberField = fields.Single(f => f.Name == "ADSK_Количество");
        Check(!numberField.Matches(1.0, double.NaN) && !numberField.Matches(1.0, double.PositiveInfinity), "Reject non-finite result");
        Console.WriteLine($"PASS: {checks} assertions; 19 distinct rows, 10 fields. No Revit API or models loaded.");
    }
}
