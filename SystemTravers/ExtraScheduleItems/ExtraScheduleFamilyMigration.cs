using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.ExtraScheduleItems
{
    internal static class ExtraScheduleFamilyMigration
    {
        internal static bool IsBuiltIn(FamilyParameter parameter) =>
            parameter.Definition is InternalDefinition definition &&
            definition.BuiltInParameter != BuiltInParameter.INVALID;

        internal static bool MakeLegacyFieldsInstance(FamilyManager manager)
        {
            bool modified = false;
            foreach (var parameter in manager.Parameters.Cast<FamilyParameter>().ToList())
            {
                // Short aliases such as Description/Mark may be built-in type parameters.
                // They are read/write aliases, not permission to change the family schema.
                if (IsBuiltIn(parameter) || parameter.IsInstance) continue;
                string name = parameter.Definition.Name;
                if (!name.StartsWith("ADSK_", StringComparison.Ordinal) ||
                    !ExtraScheduleParameterSchema.Fields.Any(f => f.Names.Contains(name))) continue;
                manager.MakeInstance(parameter);
                modified = true;
            }
            return modified;
        }

        internal static void RequireCustomParameter(FamilyParameter parameter)
        {
            if (parameter != null && IsBuiltIn(parameter))
                throw new InvalidOperationException($"Встроенный параметр {parameter.Definition.Name} нельзя заменять параметром ADSK.");
        }
    }
}
