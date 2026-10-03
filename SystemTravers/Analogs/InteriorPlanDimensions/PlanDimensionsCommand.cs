using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.Analogs.InteriorPlanDimensions
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PlanDimensionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Нет активного документа Revit.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            View activeView = doc.ActiveView;
            if (!(activeView is ViewPlan planView))
            {
                TaskDialog td = new TaskDialog("Размеры на плане")
                {
                    MainInstruction = "Неверный тип вида",
                    MainContent = "Данная команда предназначена для работы на планах (план этажа, план потолка, план зонирования).\n\nПожалуйста, перейдите на вид плана и запустите команду снова.",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning
                };
                td.Show();
                return Result.Cancelled;
            }

            var selIds = uiDoc.Selection.GetElementIds();
            bool hasSelection = selIds != null && selIds.Count > 0;

            try
            {
                PlanDimensionsWindow window = new PlanDimensionsWindow(doc, hasSelection);
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                PlanDimensionsOptions options = window.Options;
                List<Element> candidates = new List<Element>();

                if (window.ProcessOnlySelected && hasSelection)
                {
                    foreach (ElementId id in selIds)
                    {
                        Element e = doc.GetElement(id);
                        if (e is Wall || (e is FamilyInstance fi && fi.Category != null && (
                            fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Columns ||
                            fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)))
                        {
                            candidates.Add(e);
                        }
                    }
                }
                else
                {
                    // Собираем все стены и колонны на активном виде
                    var walls = new FilteredElementCollector(doc, planView.Id)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .ToElements();
                    candidates.AddRange(walls);

                    var cols = new FilteredElementCollector(doc, planView.Id)
                        .OfClass(typeof(FamilyInstance))
                        .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                        {
                            BuiltInCategory.OST_Columns,
                            BuiltInCategory.OST_StructuralColumns
                        }))
                        .ToElements();
                    candidates.AddRange(cols);
                }

                if (candidates.Count == 0)
                {
                    TaskDialog.Show("Размеры на плане", "Не найдено подходящих стен или колонн для образмеривания.");
                    return Result.Cancelled;
                }

                PlanDimensionsResult res = PlanDimensionsEngine.CreateDimensions(doc, planView, candidates, options);

                TaskDialog resDialog = new TaskDialog("Размеры на плане")
                {
                    MainInstruction = $"Создано размеров: {res.DimensionsCreated}",
                    MainContent = $"Обработано стен: {res.WallsProcessed}\nОбработано колонн: {res.ColumnsProcessed}",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    MainIcon = TaskDialogIcon.TaskDialogIconInformation
                };
                resDialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", "Ошибка при создании размеров:\n" + ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
