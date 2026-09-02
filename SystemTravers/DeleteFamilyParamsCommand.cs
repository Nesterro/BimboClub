using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class DeleteFamilyParamsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Ошибка", "Нет активного документа Revit.");
                return Result.Failed;
            }

            try
            {
                Logger.Log("Запуск команды Удаление параметров из семейств (Family Parameter Remover).");

                List<FamilyEntryItem> families = FamilyParameterService.GetProjectEditableFamilies(doc);

                if (families.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В текущем проекте не найдено загружаемых (компонентных) семейств.");
                    return Result.Cancelled;
                }

                // Если у пользователя есть предварительный выбор элементов на виде, отмечаем соответствующие семейства
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds != null && selectedIds.Count > 0)
                {
                    HashSet<ElementId> selectedFamilyIds = new HashSet<ElementId>();
                    foreach (ElementId id in selectedIds)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem is FamilyInstance fi && fi.Symbol?.Family != null)
                        {
                            selectedFamilyIds.Add(fi.Symbol.Family.Id);
                        }
                        else if (elem is FamilySymbol sym && sym.Family != null)
                        {
                            selectedFamilyIds.Add(sym.Family.Id);
                        }
                    }

                    if (selectedFamilyIds.Count > 0)
                    {
                        foreach (var f in families)
                        {
                            f.IsSelected = selectedFamilyIds.Contains(f.Id);
                        }
                    }
                }

                DeleteFamilyParamsWindow window = new DeleteFamilyParamsWindow(doc, families);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка при выполнении DeleteFamilyParamsCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
