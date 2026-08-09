using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.PipeClamps
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PipeClampsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "Не найден активный документ Revit.";
                return Result.Failed;
            }

            try
            {
                Logger.Log("Запуск модуля расстановки хомутов по вертикальным трубопроводам.");

                PipeClampsWindow window = new PipeClampsWindow(doc);
                bool? dlgResult = window.ShowDialog();

                if (dlgResult != true || !window.UserApproved)
                {
                    return Result.Cancelled;
                }

                List<PipeDiameterInfo> settings = window.ResultSettings;
                PipeClampsResult result = null;

                using (Transaction tx = new Transaction(doc, "Расстановка хомутов по стоякам"))
                {
                    tx.Start();

                    result = PipeClampsEngine.RunPlacement(doc, settings, window.ResultOptions);

                    tx.Commit();
                }

                string summaryMsg = $"Расстановка хомутов успешно завершена!\n\n" +
                                   $"• Обработано вертикальных стояков: {result.PipesProcessed}\n" +
                                   $"• Установлено хомутов: {result.ClampsPlaced}";

                TaskDialog.Show("BimboClub — Расстановка хомутов", summaryMsg);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Log($"ОШИБКА в модуле расстановки хомутов: {ex}");
                message = $"Ошибка выполнения: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
