// Test doubles only. This executable never loads Autodesk assemblies or opens models.
using System;
using System.Collections.Generic;
using System.Globalization;
namespace Autodesk.Revit.DB
{
#if NET48
    public enum BuiltInParameterGroup { INVALID, PG_DATA, PG_TEXT, PG_IDENTITY_DATA, PG_GEOMETRY, PG_MECHANICAL, PG_ELECTRICAL, PG_PLUMBING, PG_MATERIALS }
#endif
    public enum BuiltInCategory { OST_GenericModel }
    public class Category { }
    public enum StorageType { None, Integer, Double, String, ElementId }
    public class ForgeTypeId
    {
        public string Id;
        public ForgeTypeId(string id) { Id = id; }
        public override bool Equals(object other) => other is ForgeTypeId id && id.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
    public static class SpecTypeId
    {
        public static class Boolean { public static ForgeTypeId YesNo = new ForgeTypeId("yesno"); }
    }
    public class Definition
    {
        public string Name;
        public ForgeTypeId DataType = new ForgeTypeId("text");
        public ForgeTypeId GetDataType() => DataType;
    }
    public class ExternalDefinition : Definition { }
    public class FamilyParameter
    {
        public Definition Definition = new Definition();
        public StorageType StorageType;
        public string Formula;
        public bool ReadOnly;
    }
    public class FamilyType
    {
        public string Name;
        public Dictionary<FamilyParameter, object> Values = new Dictionary<FamilyParameter, object>();
        public string AsString(FamilyParameter parameter) => Values.TryGetValue(parameter, out var value) ? (string)value : null;
        public int? AsInteger(FamilyParameter parameter) => Values.TryGetValue(parameter, out var value) ? (int)value : (int?)null;
        public double? AsDouble(FamilyParameter parameter) => Values.TryGetValue(parameter, out var value) ? (double)value : (double?)null;
    }
    public class FamilyManager
    {
        public List<FamilyType> Types = new List<FamilyType>();
        public FamilyType CurrentType;
        public bool IgnoreWrites;
        public FamilyType FailOnType;
        public bool IgnoreFormula;
        public bool TruncateText;
        public FamilyType NewType(string name)
        {
            var type = new FamilyType { Name = name };
            Types.Add(type);
            CurrentType = type;
            return type;
        }
        private void Check(FamilyParameter parameter)
        {
            if (CurrentType == null) throw new InvalidOperationException("No current family type");
            if (parameter.ReadOnly) throw new InvalidOperationException("Read-only parameter");
            if (CurrentType == FailOnType) throw new InvalidOperationException("Cannot write second family type");
        }
        public void SetFormula(FamilyParameter parameter, string formula)
        {
            Check(parameter);
            if (formula == "Broken(" || formula == "2.5") throw new InvalidOperationException("Invalid formula");
            if (!IgnoreFormula) parameter.Formula = formula;
        }
        private void Store(FamilyParameter parameter, object value, StorageType storage)
        {
            Check(parameter);
            if (!string.IsNullOrEmpty(parameter.Formula)) throw new InvalidOperationException("Parameter is controlled by formula");
            if (parameter.StorageType != storage) throw new ArgumentException("Wrong storage overload");
            if (!IgnoreWrites) CurrentType.Values[parameter] = value;
        }
        public void Set(FamilyParameter parameter, string value) => Store(parameter, TruncateText ? value.Substring(0, 1) : value, StorageType.String);
        public void Set(FamilyParameter parameter, int value) => Store(parameter, value, StorageType.Integer);
        public void Set(FamilyParameter parameter, double value) => Store(parameter, value, StorageType.Double);
    }
    public class Units { }
    public class Document
    {
        public FamilyManager FamilyManager = new FamilyManager();
        public Units GetUnits() => new Units();
    }
    public static class UnitFormatUtils
    {
        public static bool TryParse(Units units, ForgeTypeId type, string text, out double value)
        {
            string cleaned = text.Replace(" мм", "").Replace(',', '.');
            bool ok = double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (type.Id == "length") value /= 304.8;
            return ok;
        }
    }
}
