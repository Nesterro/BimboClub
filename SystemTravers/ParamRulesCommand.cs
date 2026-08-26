using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimboClub.Rules;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ParamRulesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null)
                {
                    TaskDialog.Show("BimboClub", "Откройте проект Revit перед запуском редактора правил.");
                    return Result.Cancelled;
                }

                // Run modern native BimboClub Rule Editor Window
                var window = new RuleEditorWindow(doc, uidoc);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("BimboClub | Ошибка", $"Ошибка при запуске Редактора правил:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
