using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.Analogs.CopyElements
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CopyElementsCommand : IExternalCommand
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
                TaskDialog td = new TaskDialog("Копировать элементы в документы")
                {
                    MainInstruction = "Недостаточно открытых проектов",
                    MainContent = "Для копирования элементов и стандартов необходимо, чтобы в Revit было открыто не менее двух проектов (проект-источник и проект-приёмник).\n\nПожалуйста, откройте целевой проект и повторите запуск команды.",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning
                };
                td.Show();
                return Result.Cancelled;
            }

            try
            {
                CopyElementsWindow window = new CopyElementsWindow(uiApp);
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                Document sourceDoc = window.SelectedSourceDoc;
                List<ElementId> elementIds = window.SelectedElementIds;
                List<Document> targetDocs = window.SelectedTargetDocs;
                CopyElementsOptions options = window.Options;

                if (sourceDoc == null || elementIds.Count == 0 || targetDocs.Count == 0)
                {
                    return Result.Cancelled;
                }

                int totalCopied = 0;
                var report = new StringBuilder();

                foreach (Document targetDoc in targetDocs)
                {
                    CopyElementsResult res = CopyElementsEngine.CopyElements(sourceDoc, elementIds, targetDoc, options);
                    totalCopied += res.CopiedCount;

                    report.AppendLine($"Документ «{targetDoc.Title}»:");
                    report.AppendLine($"  - Скопировано элементов: {res.CopiedCount}");
                    if (res.Messages.Count > 0)
                    {
                        foreach (var m in res.Messages)
                        {
                            report.AppendLine($"    [Предупреждение] {m}");
                        }
                    }
                }

                TaskDialog resultDialog = new TaskDialog("Копирование элементов завершено")
                {
                    MainInstruction = $"Успешно скопировано элементов: {totalCopied}",
                    MainContent = $"Целевых проектов: {targetDocs.Count}\nКопируемых элементов: {elementIds.Count}\n\n{report}",
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    MainIcon = TaskDialogIcon.TaskDialogIconInformation
                };
                resultDialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", "Ошибка при копировании элементов:\n" + ex.Message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
