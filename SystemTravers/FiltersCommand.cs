using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class FiltersCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Запуск команды Копирование фильтров видов (Filters Copier).");

                // 1. Собираем все виды в проекте, которые поддерживают фильтры
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc);
                List<View> allViews = viewCollector
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.AreGraphicsOverridesAllowed())
                    .OrderBy(v => v.Name)
                    .ToList();

                if (allViews.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В текущем проекте не найдено видов, поддерживающих фильтры графики.");
                    return Result.Cancelled;
                }

                // 2. Отделяем виды-доноры (у которых наложено хотя бы один фильтр)
                List<ViewItem> sourceViews = allViews
                    .Where(v => v.GetFilters().Count > 0)
                    .Select(v => new ViewItem { Name = $"{v.Name} ({GetViewTypeName(v)})", RawView = v })
                    .ToList();

                if (sourceViews.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В проекте не найдено ни одного вида с настроенными и примененными фильтрами.");
                    return Result.Cancelled;
                }

                // 3. Формируем список целевых видов
                List<TargetViewItem> targetViews = allViews
                    .Select(v => new TargetViewItem 
                    { 
                        Name = v.Name, 
                        ViewType = GetViewTypeName(v), 
                        RawView = v 
                    })
                    .ToList();

                // Исходный активный вид по умолчанию
                View activeView = doc.ActiveView;

                // 4. Колбэк для динамического получения фильтров при смене исходного вида
                Func<object, List<FilterItem>> onSourceViewChanged = (rawView) =>
                {
                    View view = rawView as View;
                    if (view == null) return new List<FilterItem>();

                    ICollection<ElementId> filterIds = view.GetFilters();
                    List<FilterItem> filters = new List<FilterItem>();
                    foreach (ElementId fid in filterIds)
                    {
                        Element filterElem = doc.GetElement(fid);
                        if (filterElem != null)
                        {
                            filters.Add(new FilterItem 
                            { 
                                Name = filterElem.Name, 
                                RawFilter = fid 
                            });
                        }
                    }
                    return filters.OrderBy(f => f.Name).ToList();
                };

                // 5. Запуск WPF-интерфейса
                FiltersWindow window = new FiltersWindow(sourceViews, targetViews, activeView, onSourceViewChanged);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true && window.IsCopyRequested)
                {
                    View sourceView = window.SelectedSourceView as View;
                    List<ElementId> selectedFilterIds = window.SelectedFilters.Select(f => (ElementId)f.RawFilter).ToList();
                    List<View> selectedTargetViews = window.SelectedTargetViews.Select(t => t.RawView as View).Where(v => v != null).ToList();

                    if (sourceView == null || selectedFilterIds.Count == 0 || selectedTargetViews.Count == 0)
                    {
                        return Result.Cancelled;
                    }

                    int copiedFiltersCount = 0;

                    // 6. Выполнение копирования в транзакции
                    using (Transaction tx = new Transaction(doc, "Копирование фильтров видов"))
                    {
                        tx.Start();

                        foreach (View targetView in selectedTargetViews)
                        {
                            // Пропускаем сам вид-донор, если он был выбран в качестве цели
                            if (targetView.Id == sourceView.Id) continue;

                            foreach (ElementId fid in selectedFilterIds)
                            {
                                try
                                {
                                    // 1. Добавляем фильтр, если его нет
                                    if (!targetView.GetFilters().Contains(fid))
                                    {
                                        targetView.AddFilter(fid);
                                    }

                                    // 2. Копируем параметры видимости
                                    bool isVisible = sourceView.GetFilterVisibility(fid);
                                    targetView.SetFilterVisibility(fid, isVisible);

                                    // 3. Копируем активность фильтра (доступно с Revit 2019)
                                    bool isEnabled = sourceView.GetIsFilterEnabled(fid);
                                    targetView.SetIsFilterEnabled(fid, isEnabled);

                                    // 4. Копируем настройки переопределения графики
                                    OverrideGraphicSettings overrides = sourceView.GetFilterOverrides(fid);
                                    targetView.SetFilterOverrides(fid, overrides);

                                    copiedFiltersCount++;
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Не удалось скопировать фильтр {fid} на вид {targetView.Name}", ex);
                                }
                            }
                        }

                        tx.Commit();
                    }

                    Logger.Log($"Успешно скопировано фильтров: {copiedFiltersCount} на {selectedTargetViews.Count} видов.");
                    TaskDialog.Show("Успех", $"Фильтры успешно скопированы!\nВидов обновлено: {selectedTargetViews.Count}\nПеренесено настроек фильтрации: {copiedFiltersCount}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения FiltersCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private string GetViewTypeName(View view)
        {
            switch (view.ViewType)
            {
                case ViewType.ThreeD: return "3D вид";
                case ViewType.FloorPlan: return "План этажа";
                case ViewType.CeilingPlan: return "План потолка";
                case ViewType.Elevation: return "Фасад";
                case ViewType.Section: return "Разрез";
                case ViewType.EngineeringPlan: return "План несущих констр.";
                case ViewType.AreaPlan: return "План зонирования";
                case ViewType.Detail: return "Фрагмент";
                default: return view.ViewType.ToString();
            }
        }
    }
}
