using System;
using System.Collections.Generic;

namespace Autodesk.Revit.DB
{
    public enum BuiltInParameter { RBS_SYSTEM_NAME_PARAM = -1140324 }
    public enum StorageType { None, Integer, Double, String, ElementId }
    public sealed class ElementId
    {
        public static readonly ElementId InvalidElementId = new ElementId(-1);
        public int IntegerValue { get; }
        public ElementId(int value) { IntegerValue = value; }
        public override bool Equals(object other) => other is ElementId id && id.IntegerValue == IntegerValue;
        public override int GetHashCode() => IntegerValue;
        public override string ToString() => IntegerValue.ToString();
    }
    public class Definition { public string Name { get; set; } }
    public class Parameter
    {
        public StorageType StorageType { get; set; } = StorageType.String;
        public bool HasValue { get; set; } = true;
        public string Value { get; set; }
        public int Reads { get; private set; }
        public bool Fail { get; set; }
        public string AsString()
        {
            Reads++;
            if (Fail || !HasValue || StorageType != StorageType.String)
                throw new InvalidOperationException("Invalid direct parameter read");
            return Value;
        }
    }
    public class Element
    {
        public ElementId Id { get; set; }
        public ElementId TypeId { get; set; } = ElementId.InvalidElementId;
        public Dictionary<BuiltInParameter, Parameter> BuiltIns { get; } = new Dictionary<BuiltInParameter, Parameter>();
        public Dictionary<Definition, Parameter> Defined { get; } = new Dictionary<Definition, Parameter>();
        public Parameter get_Parameter(BuiltInParameter id) => BuiltIns.TryGetValue(id, out var p) ? p : null;
        public Parameter get_Parameter(Definition definition) => Defined.TryGetValue(definition, out var p) ? p : null;
        public ElementId GetTypeId() => TypeId;
    }
    public class ParameterElement : Element
    {
        public Definition Definition { get; set; }
        public Definition GetDefinition() => Definition;
    }
    public class Document
    {
        public Dictionary<ElementId, Element> Elements { get; } = new Dictionary<ElementId, Element>();
        public Element GetElement(ElementId id) => Elements.TryGetValue(id, out var element) ? element : null;
    }
}
