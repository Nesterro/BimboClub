using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.RuleEditor
{
    [Transaction(TransactionMode.Manual)]
    public class RuleEditorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                RuleEditorWindow window = new RuleEditorWindow(doc, uidoc);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true && window.IsRunRequested)
                {
                    RuleEditorEngine engine = new RuleEditorEngine(doc);
                    RuleExecutionResult execRes = engine.ExecuteRuleSet(window.RuleSet, window.CurrentViewOnly);

                    string summary = $"Обработка по правилам завершена!\n\n" +
                                     $"Обработано элементов: {execRes.TotalProcessed}\n" +
                                     $"Обновлено параметров: {execRes.TotalUpdated}";

                    TaskDialog.Show("Редактор правил - BimboClub", summary);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка при выполнении: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
