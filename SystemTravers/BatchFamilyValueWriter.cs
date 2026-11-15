using System;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub
{
    // Called inside the batch transaction, after ALL parameter definitions exist.
    internal static class BatchFamilyValueWriter
    {
        internal static Action Apply(Document doc, FamilyParameter parameter, SharedParamItem item)
        {
            string explicitFormula = item.Formula;
            string input = item.DefaultValue ?? "";
            if (string.IsNullOrWhiteSpace(explicitFormula) && string.IsNullOrWhiteSpace(input)) return null;

            var manager = doc.FamilyManager;
            if (manager.CurrentType == null)
                manager.CurrentType = manager.Types.Cast<FamilyType>().FirstOrDefault() ?? manager.NewType("Тип 1");

            bool isFormula = !string.IsNullOrWhiteSpace(explicitFormula) || input.TrimStart().StartsWith("=", StringComparison.Ordinal);
            string formula = !string.IsNullOrWhiteSpace(explicitFormula) ? explicitFormula.Trim() : input.Trim();
            if (formula.StartsWith("=", StringComparison.Ordinal)) formula = formula.Substring(1).Trim();

            if (isFormula) return ApplyFormula(manager, parameter, formula);

            object value;
            string trimmed = input.Trim();
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    // Keep compatibility with quoted text in old presets, including "" for clearing.
                    value = trimmed.Length >= 2 && trimmed.StartsWith("\"") && trimmed.EndsWith("\"")
                        ? trimmed.Substring(1, trimmed.Length - 2) : input;
                    value = ((string)value).Trim(); // Revit trims outer whitespace on text values.
                    break;
                case StorageType.Integer:
                    bool yesNo = parameter.Definition.GetDataType().Equals(SpecTypeId.Boolean.YesNo);
                    if (yesNo && TryBoolean(trimmed, out int flag)) value = flag;
                    else if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                    {
                        if (yesNo && integer != 0 && integer != 1)
                            throw new InvalidOperationException("Для параметра Да/Нет введите Да, Нет, 1 или 0.");
                        value = integer;
                    }
                    else return ApplyFormula(manager, parameter, trimmed); // Legacy numeric expressions.
                    break;
                case StorageType.Double:
                    // Parse displayed units, never treat millimetres as internal feet.
                    if (UnitFormatUtils.TryParse(doc.GetUnits(), parameter.Definition.GetDataType(), trimmed, out double number))
                        value = number;
                    else return ApplyFormula(manager, parameter, trimmed);
                    break;
                default:
                    throw new InvalidOperationException("Для этого типа параметра задайте формулу через '=' или оставьте значение пустым.");
            }

            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                throw new InvalidOperationException("Значение должно быть конечным числом.");

            // A supplied literal replaces an old formula. Do not silently fall back to stale data.
            if (!string.IsNullOrEmpty(parameter.Formula)) manager.SetFormula(parameter, null);
            var types = manager.Types.Cast<FamilyType>().ToList();
            var currentType = manager.CurrentType;
            try
            {
                foreach (var type in types)
                {
                    manager.CurrentType = type;
                    switch (parameter.StorageType)
                    {
                        case StorageType.String: manager.Set(parameter, (string)value); break;
                        case StorageType.Integer: manager.Set(parameter, (int)value); break;
                        case StorageType.Double: manager.Set(parameter, (double)value); break;
                    }
                }
            }
            finally { manager.CurrentType = currentType; }

            // Run after the whole batch regenerates: detect rejected/truncated/overwritten values.
            return () =>
            {
                if (!string.IsNullOrEmpty(parameter.Formula))
                    throw new InvalidOperationException("Вместо заданного значения сохранилась формула.");
                foreach (var type in types)
                {
                    bool matches;
                    switch (parameter.StorageType)
                    {
                        case StorageType.String:
                            matches = string.Equals(type.AsString(parameter) ?? "", (string)value, StringComparison.Ordinal);
                            break;
                        case StorageType.Integer:
                            matches = type.AsInteger(parameter) == (int)value;
                            break;
                        case StorageType.Double:
                            double? actual = type.AsDouble(parameter);
                            double expected = (double)value;
                            matches = actual.HasValue && Math.Abs(actual.Value - expected) <= 1e-9 * Math.Max(1, Math.Abs(expected));
                            break;
                        default: matches = false; break;
                    }
                    if (!matches)
                        throw new InvalidOperationException($"Значение не сохранилось в типоразмере «{type.Name}».");
                }
            };
        }

        private static Action ApplyFormula(FamilyManager manager, FamilyParameter parameter, string formula)
        {
            if (string.IsNullOrWhiteSpace(formula)) throw new InvalidOperationException("После '=' должна быть формула.");
            manager.SetFormula(parameter, formula);
            return () =>
            {
                if (string.IsNullOrWhiteSpace(parameter.Formula))
                    throw new InvalidOperationException("Revit не сохранил заданную формулу.");
            };
        }

        private static bool TryBoolean(string text, out int value)
        {
            if (string.Equals(text, "да", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            { value = 1; return true; }
            if (string.Equals(text, "нет", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            { value = 0; return true; }
            value = 0;
            return false;
        }
    }
}
