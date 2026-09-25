using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BimboClub.SpacesFromRooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SpacesFromRoomsCommand : IExternalCommand
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
                // Проверяем наличие связей с помещениями перед открытием окна
                var links = SpacesFromRoomsService.GetLinksWithRooms(doc);
                if (links.Count == 0)
                {
                    TaskDialog.Show("Пространства по АР",
                        "В проекте не найдено связанных файлов (RevitLinkInstance), содержащих размещенные помещения.\n\n" +
                        "Убедитесь, что архитектурная модель загружена в диспетчере связей и в ней созданы помещения.");
                    return Result.Cancelled;
                }

                SpacesFromRoomsWindow window = new SpacesFromRoomsWindow(doc);
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                if (window.ShowDialog() != true || window.Settings == null)
                {
                    return Result.Cancelled;
                }

                SpaceCreationResult result = SpacesFromRoomsService.CreateSpaces(doc, window.Settings);

                string report = $"Расстановка пространств завершена:\n\n" +
                               $"• Создано новых пространств: {result.CreatedCount}\n" +
                               $"• Обновлено параметров существующих: {result.UpdatedCount}\n" +
                               $"• Пропущено (дубликаты / вне контура): {result.SkippedCount}";

                if (result.ErrorCount > 0)
                {
                    report += $"\n• Ошибок при создании: {result.ErrorCount}";
                }

                TaskDialog.Show("Пространства по АР", report);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде SpacesFromRoomsCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
