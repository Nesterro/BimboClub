using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.Analogs.BaseLevel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BaseLevelCommand : IExternalCommand
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
            var selectedIds = uiDoc.Selection.GetElementIds();
            bool hasPreselected = selectedIds != null && selectedIds.Count > 0;

            try
            {
                BaseLevelWindow window = new BaseLevelWindow(doc, hasPreselected);
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                var config = window.CurrentConfiguration;
                if (config == null || config.Rules == null || config.Rules.Count == 0)
                {
                    TaskDialog.Show("Базовый уровень", "В конфигурации нет правил для применения.");
                    return Result.Cancelled;
                }

                // 1. Сбор целевых элементов
                List<Element> candidates = new List<Element>();
                if (window.Scope == ProcessScope.SelectedElements && hasPreselected)
                {
                    foreach (ElementId id in selectedIds)
                    {
                        Element e = doc.GetElement(id);
                        if (e != null && e.Category != null && e.Category.CategoryType == CategoryType.Model)
                        {
                            candidates.Add(e);
                        }
                    }
                }
                else if (window.Scope == ProcessScope.ActiveView)
                {
                    candidates = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                        .ToList();
                }
                else
                {
                    candidates = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                        .ToList();
                }

                if (candidates.Count == 0)
                {
                    TaskDialog.Show("Базовый уровень", "Не найдено элементов модели для обработки в выбранной области.");
                    return Result.Succeeded;
                }

                // 2. Сбор уровней
                var allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                List<Level> activeLevels = allLevels;
                if (window.OnlyVisibleLevels && doc.ActiveView != null)
                {
                    var visibleLevelIds = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfClass(typeof(Level))
                        .ToElementIds();

                    if (visibleLevelIds.Count > 0)
                    {
                        activeLevels = allLevels.Where(l => visibleLevelIds.Contains(l.Id)).ToList();
                    }
                }

                if (activeLevels.Count == 0)
                {
                    TaskDialog.Show("Базовый уровень", "В проекте не найдено доступных уровней.");
                    return Result.Failed;
                }

                // 3. Выполнение пакетной перепривязки
                int updatedCount = 0;

                using (Transaction tr = new Transaction(doc, "BimboClub: Базовый уровень"))
                {
                    tr.Start();

                    foreach (Element elem in candidates)
                    {
                        if (elem.Pinned) continue;

                        foreach (var rule in config.Rules)
                        {
                            if (BaseLevelEngine.ElementMatchesFilter(elem, rule))
                            {
                                bool applied = BaseLevelEngine.ApplyRuleToElement(doc, elem, rule, activeLevels);
                                if (applied)
                                {
                                    updatedCount++;
                                    break; // Применили первое подошедшее правило для этого элемента
                                }
                            }
                        }
                    }

                    tr.Commit();
                }

                TaskDialog.Show(
                    "Базовый уровень",
                    $"Обработка завершена!\n\n" +
                    $"Всего проверено элементов: {candidates.Count}\n" +
                    $"Успешно обновлено уровней: {updatedCount}\n" +
                    $"Физическая геометрия элементов сохранена без изменений.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Базовый уровень", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
