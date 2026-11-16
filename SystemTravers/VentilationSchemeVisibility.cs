using Autodesk.Revit.DB;
using System.Linq;

namespace BimboClub
{
    internal static class VentilationSchemeVisibility
    {
        // Model categories: hiding a parent also hides its geometry subcategories.
        // Mechanical Equipment belongs to both disciplines and must remain enabled.
        private static readonly BuiltInCategory[] HiddenCategories =
        {
            BuiltInCategory.OST_RvtLinks,
            BuiltInCategory.OST_Coordination_Model,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PlaceHolderPipes,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_PipeSegments,
            BuiltInCategory.OST_PipingSystem,
            BuiltInCategory.OST_FabricationPipework,
            BuiltInCategory.OST_PlumbingEquipment,
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_Sprinklers
        };

        internal static void Apply(Document document, View3D view)
        {
            foreach (var category in HiddenCategories)
                SetHidden(view, Category.GetCategory(document, category), true);

            // Linked CAD files have their own categories rather than OST_RvtLinks.
            foreach (var link in new FilteredElementCollector(document)
                .OfClass(typeof(ImportInstance)).Cast<ImportInstance>().Where(x => x.IsLinked))
                SetHidden(view, link.Category, true);

            SetHidden(view, Category.GetCategory(document, BuiltInCategory.OST_MechanicalEquipment), false);
        }

        private static void SetHidden(View view, Category category, bool hidden)
        {
            // Some internal categories (e.g. pipe segments) have no visibility control.
            // Do not use CanAddSubcategory: links and other hideable categories can lack it.
            if (category != null && view.CanCategoryBeHidden(category.Id))
                view.SetCategoryHidden(category.Id, hidden);
        }
    }
}
