using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.Analogs.CopySheets
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CopySheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            var openDocs = uiApp.Application.Documents
                .Cast<Document>()
                .Where(d => !d.IsFamilyDocument)
                .ToList();

            if (openDocs.Count < 2)
            {
                TaskDialog td = new TaskDialog("Копировать листы в документы")
                {
                    MainInstruction = "Недостаточно открытых проектов",
                    MainContent = "Для копирования листов необходимо, чтобы в Revit было открыто как минимум два проекта (проект-источник и проект-приёмник).\n\nПожалуйста, откройте целевой проект и повторите запуск команды.",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning
                };
                td.Show();
                return Result.Cancelled;
            }

            try
            {
                CopySheetsWindow window = new CopySheetsWindow(uiApp);
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                Document sourceDoc = window.SelectedSourceDoc;
                List<ViewSheet> selectedSheets = window.SelectedSheets;
                List<Document> targetDocs = window.SelectedTargetDocs;
                CopySheetsOptions options = window.Options;

                if (sourceDoc == null || selectedSheets.Count == 0 || targetDocs.Count == 0)
                {
                    return Result.Cancelled;
                }

                int totalCreated = 0;
                int totalSkipped = 0;
                int totalViews = 0;
                int totalSchedules = 0;
                var report = new StringBuilder();

                foreach (Document targetDoc in targetDocs)
                {
                    CopySheetsResult res = CopySheetsEngine.CopySheets(sourceDoc, selectedSheets, targetDoc, options);
                    totalCreated += res.SheetsCreated;
                    totalSkipped += res.SheetsSkipped;
                    totalViews += res.ViewsCopied;
                    totalSchedules += res.SchedulesPlaced;

                    report.AppendLine($"Документ «{targetDoc.Title}»:");
                    report.AppendLine($"  - Создано/обновлено листов: {res.SheetsCreated}");
                    if (res.SheetsSkipped > 0)
                        report.AppendLine($"  - Пропущено листов: {res.SheetsSkipped}");
                    if (res.ViewsCopied > 0)
                        report.AppendLine($"  - Перенесено видов: {res.ViewsCopied}");
                    if (res.SchedulesPlaced > 0)
                        report.AppendLine($"  - Размещено спецификаций: {res.SchedulesPlaced}");
                }

                TaskDialog resultDialog = new TaskDialog("Копирование листов завершено")
                {
                    MainInstruction = $"Успешно обработано листов: {totalCreated} (пропущено: {totalSkipped})",
                    MainContent = $"Целевых проектов: {targetDocs.Count}\nПеренесено видов: {totalViews}\nРазмещено спецификаций: {totalSchedules}\n\n{report}",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    MainIcon = TaskDialogIcon.TaskDialogIconInformation
                };
                resultDialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", "Ошибка при копировании листов:\n" + ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
