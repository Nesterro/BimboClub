using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.Analogs.FontReplacer
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FontReplacerCommand : IExternalCommand
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

            try
            {
                FontReplacerWindow window = new FontReplacerWindow(doc);
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                var settings = window.Settings;
                if (settings == null) return Result.Cancelled;

                var stats = FontReplacerEngine.ExecuteReplacement(
                    doc,
                    settings,
                    window.SelectedTextTypeIds,
                    window.SelectedDimTypeIds,
                    window.SelectedScheduleIds,
                    window.SelectedFamilyIds);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Целевой шрифт: {settings.TargetFontName}\n");
                sb.AppendLine($"• Текстовых стилей обновлено: {stats.TextNoteTypesUpdated}");
                sb.AppendLine($"• Размерных стилей обновлено: {stats.DimensionTypesUpdated}");
                sb.AppendLine($"• Спецификаций обновлено: {stats.SchedulesUpdated}");
                sb.AppendLine($"• Семейств аннотаций/марок перезагружено: {stats.FamiliesUpdated}");

                if (stats.Warnings.Count > 0)
                {
                    sb.AppendLine("\nПредупреждения:");
                    foreach (var w in stats.Warnings.Take(5))
                    {
                        sb.AppendLine("— " + w);
                    }
                    if (stats.Warnings.Count > 5)
                    {
                        sb.AppendLine($"...и еще {stats.Warnings.Count - 5} предупреждений.");
                    }
                }

                TaskDialog.Show("Замена шрифта", sb.ToString());
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Заменить шрифт", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
