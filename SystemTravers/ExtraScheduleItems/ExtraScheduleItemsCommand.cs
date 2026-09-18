using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.ExtraScheduleItems
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExtraScheduleItemsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Нет активного документа Revit.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;

            try
            {
                ExtraScheduleItemsWindow window = new ExtraScheduleItemsWindow(doc);
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка при выполнении команды Допы в спецификацию", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
