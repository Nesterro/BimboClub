// Minimal test double: no Autodesk assemblies, processes, models or AppData access.
// Compile the actual production migration loop against this API surface.
using System;
using System.Collections.Generic;

namespace Autodesk.Revit.DB
{
    internal enum BuiltInParameter { INVALID = -1, ALL_MODEL_DESCRIPTION = -1010103, ALL_MODEL_TYPE_MARK = -1001405 }
    internal class Definition { public string Name { get; set; } }
    internal sealed class InternalDefinition : Definition
    {
        public BuiltInParameter BuiltInParameter { get; set; } = BuiltInParameter.INVALID;
    }
    internal sealed class FamilyParameter
    {
        public Definition Definition { get; set; }
        public bool IsInstance { get; set; }
    }
    internal sealed class FamilyManager
    {
        public List<FamilyParameter> Parameters { get; } = new List<FamilyParameter>();
        public List<FamilyParameter> Converted { get; } = new List<FamilyParameter>();
        public FamilyParameter FailOn { get; set; }
        public void MakeInstance(FamilyParameter parameter)
        {
            if (parameter.Definition is InternalDefinition definition && definition.BuiltInParameter != BuiltInParameter.INVALID)
                throw new ArgumentException("A BuiltInParameter cannot be changed from instance to type or type to instance.", "familyParameter");
            if (parameter == FailOn) throw new InvalidOperationException("Real ADSK conversion failure");
            Converted.Add(parameter);
            parameter.IsInstance = true;
        }
    }
}
