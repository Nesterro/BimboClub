using System;
using System.IO;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.HeatLoss
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HeatLossPrepCommand : IExternalCommand
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
                Logger.Log("Запуск модуля подготовки данных для расчета теплопотерь.");

                // 1. Открытие WPF окна настроек
                HeatLossPrepWindow window = new HeatLossPrepWindow(doc);
                bool? dlgResult = window.ShowDialog();

                if (dlgResult != true || !window.UserApproved)
                {
                    Logger.Log("Операция отменена пользователем.");
                    return Result.Cancelled;
                }

                HeatLossAnalysisOptions options = window.SelectedOptions;
                HeatLossResult result = null;

                // 2. Выполнение алгоритма в транзакции
                using (Transaction tx = new Transaction(doc, "Подготовка данных для расчета теплопотерь"))
                {
                    tx.Start();

                    // Запуск основного движка анализа и размещения кубиков
                    result = HeatLossAnalyzer.Run(doc, options, (pct, msg) =>
                    {
                        window.UpdateProgress(pct, msg);
                    });

                    // Создание или обновление Спецификации по категории Обобщенная модель
                    HeatLossScheduleService.EnsureScheduleExists(doc);

                    tx.Commit();
                }

                // 3. Вывод итогового результата
                string summaryMsg = $"Расчет и подготовка данных теплопотерь успешно завершены!\n\n" +
                                   $"• Создано Пространств (Spaces): {result.SpacesCreated}\n" +
                                   $"• Размещено кубиков-маркеров: {result.CubesPlaced}\n" +
                                   $"• Предупреждений/пропусков: {result.WarningsCount}\n\n" +
                                   $"Автоматически создана Спецификация:\n«{HeatLossScheduleService.SCHEDULE_NAME}»";

                TaskDialog.Show("BimboClub — Расчет теплопотерь", summaryMsg);
                Logger.Log($"Завершено успешно. Кубиков размещено: {result.CubesPlaced}, Пространств создано: {result.SpacesCreated}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Log($"ОШИБКА в модуля теплопотерь: {ex}");
                message = $"Ошибка выполнения: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
