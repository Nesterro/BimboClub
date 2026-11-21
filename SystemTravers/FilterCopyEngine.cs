using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub
{
    public class FilterCopyEngine
    {
        private readonly Document _doc;

        public FilterCopyEngine(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public List<SourceViewItem> GetSourceViews()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.AreGraphicsOverridesAllowed())
                .Select(v => new SourceViewItem
                {
                    ViewId = v.Id,
                    ViewName = v.Name,
                    ViewType = v.ViewType,
                    IsTemplate = v.IsTemplate,
                    FilterCount = v.GetFilters().Count
                })
                .Where(v => v.FilterCount > 0)
                .OrderBy(v => v.IsTemplate)
                .ThenBy(v => v.ViewName)
                .ToList();
        }

        public List<BccFilterCopyItem> GetViewFilters(ElementId sourceViewId)
        {
            List<BccFilterCopyItem> list = new List<BccFilterCopyItem>();
            if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId) return list;

            View sourceView = _doc.GetElement(sourceViewId) as View;
            if (sourceView == null || !sourceView.AreGraphicsOverridesAllowed()) return list;

            ICollection<ElementId> filterIds = sourceView.GetFilters();
            foreach (ElementId fId in filterIds)
            {
                Element filterElem = _doc.GetElement(fId);
                if (filterElem == null) continue;

                bool isVisible = sourceView.GetFilterVisibility(fId);
                bool isEnabled = sourceView.GetIsFilterEnabled(fId);
                OverrideGraphicSettings overrides = sourceView.GetFilterOverrides(fId);
                bool isHalftone = overrides != null ? overrides.Halftone : false;
                string summary = BuildOverridesSummary(overrides, isVisible, isEnabled, isHalftone);

                list.Add(new BccFilterCopyItem
                {
                    FilterId = fId,
                    FilterName = filterElem.Name,
                    IsVisible = isVisible,
                    IsEnabled = isEnabled,
                    IsHalftone = isHalftone,
                    GraphicOverrides = overrides,
                    OverridesSummary = summary,
                    IsSelected = true
                });
            }

            return list.OrderBy(f => f.FilterName).ToList();
        }

        public List<BccTargetViewItem> GetTargetViews(ElementId excludeSourceId)
        {
            List<BccTargetViewItem> list = new List<BccTargetViewItem>();

            List<View> views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.Id != excludeSourceId && v.AreGraphicsOverridesAllowed())
                .OrderBy(v => v.IsTemplate ? 1 : 0)
                .ThenBy(v => v.Name)
                .ToList();

            foreach (View v in views)
            {
                bool hasTemplate = (v.ViewTemplateId != null && v.ViewTemplateId != ElementId.InvalidElementId);
                string tmplName = "";
                if (hasTemplate)
                {
                    View tmpl = _doc.GetElement(v.ViewTemplateId) as View;
                    if (tmpl != null) tmplName = tmpl.Name;
                }

                list.Add(new BccTargetViewItem
                {
                    ViewId = v.Id,
                    ViewName = v.Name,
                    ViewType = v.ViewType,
                    IsTemplate = v.IsTemplate,
                    HasTemplateApplied = hasTemplate,
                    TemplateId = v.ViewTemplateId,
                    TemplateName = tmplName,
                    IsSelected = false
                });
            }

            return list;
        }

        public FilterCopyResult ExecuteCopy(
            ElementId sourceViewId,
            List<BccFilterCopyItem> selectedFilters,
            List<BccTargetViewItem> selectedTargets,
            FilterCopyOptions options)
        {
            FilterCopyResult result = new FilterCopyResult();
            if (sourceViewId == null || selectedFilters == null || selectedTargets == null)
            {
                result.Message = "Выберите источник, фильтры и целевые виды.";
                return result;
            }

            List<BccFilterCopyItem> filters = selectedFilters.Where(filter => filter.IsSelected).ToList();
            HashSet<ElementId> targetIds = new HashSet<ElementId>();
            foreach (BccTargetViewItem item in selectedTargets.Where(target => target.IsSelected))
            {
                if (!(_doc.GetElement(item.ViewId) is View view)) continue;

                targetIds.Add(view.Id);
                if (options.CopyToAppliedTemplates && !view.IsTemplate)
                {
                    targetIds.Add(view.ViewTemplateId);
                }
            }

            targetIds.Remove(sourceViewId);
            targetIds.Remove(ElementId.InvalidElementId);
            if (filters.Count == 0 || targetIds.Count == 0)
            {
                result.Message = "Выберите фильтры и целевые виды.";
                return result;
            }

            using (Transaction tx = new Transaction(_doc, "BimboClub — Копирование фильтров видов"))
            {
                tx.Start();

                foreach (ElementId viewId in targetIds)
                {
                    if (!(_doc.GetElement(viewId) is View view) || !view.AreGraphicsOverridesAllowed()) continue;

                    ICollection<ElementId> existingFilters = view.GetFilters();
                    foreach (BccFilterCopyItem filter in filters)
                    {
                        ElementId filterId = filter.FilterId;
                        bool filterExists = existingFilters.Contains(filterId);

                        if (filterExists && !options.OverwriteExistingFilters)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        try
                        {
                            using (SubTransaction change = new SubTransaction(_doc))
                            {
                                change.Start();

                                if (!filterExists)
                                {
                                    view.AddFilter(filterId);
                                }
                                if (options.CopyVisibility)
                                {
                                    view.SetFilterVisibility(filterId, filter.IsVisible);
                                }
                                if (options.CopyEnabledState)
                                {
                                    view.SetIsFilterEnabled(filterId, filter.IsEnabled);
                                }
                                if (options.CopyGraphicOverrides && filter.GraphicOverrides != null)
                                {
                                    using (OverrideGraphicSettings overrides = new OverrideGraphicSettings(filter.GraphicOverrides))
                                    {
                                        if (!options.CopyHalftone)
                                        {
                                            using (OverrideGraphicSettings currentOverrides = view.GetFilterOverrides(filterId))
                                            {
                                                overrides.SetHalftone(currentOverrides.Halftone);
                                            }
                                        }
                                        view.SetFilterOverrides(filterId, overrides);
                                    }
                                }

                                if (change.Commit() != TransactionStatus.Committed)
                                {
                                    throw new InvalidOperationException("Revit отменил изменение фильтра.");
                                }
                            }

                            result.CopiedFilterInstances++;
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"Вид [{view.Name}]. Фильтр [{filter.FilterName}]: {ex.Message}");
                        }
                    }

                    result.ProcessedViewsCount++;
                }

                if (tx.Commit() != TransactionStatus.Committed)
                {
                    result.CopiedFilterInstances = 0;
                    result.Message = "Revit не сохранил изменения фильтров.";
                    return result;
                }
            }

            result.Success = true;
            result.Message = $"Скопировано настроек фильтров: {result.CopiedFilterInstances}. Обработано видов и шаблонов: {result.ProcessedViewsCount}.";
            return result;
        }

        private static string BuildOverridesSummary(OverrideGraphicSettings ogs, bool isVis, bool isEnabled, bool isHalftone)
        {
            if (ogs == null) return isEnabled ? "Включён" : "Выключен";

            List<string> parts = new List<string>();
            parts.Add(isEnabled ? "Включён" : "Выключен");
            parts.Add(isVis ? "Видимый" : "Скрыт");
            if (isHalftone) parts.Add("Полутон");

            try
            {
                if (ogs.ProjectionLineColor.IsValid) parts.Add($"Линии: #{ogs.ProjectionLineColor.Red:X2}{ogs.ProjectionLineColor.Green:X2}{ogs.ProjectionLineColor.Blue:X2}");
            }
            catch { }

            try
            {
                if (ogs.CutForegroundPatternColor.IsValid) parts.Add($"Разрез: #{ogs.CutForegroundPatternColor.Red:X2}{ogs.CutForegroundPatternColor.Green:X2}{ogs.CutForegroundPatternColor.Blue:X2}");
            }
            catch { }

            return string.Join(", ", parts);
        }
    }

    public class FilterCopyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ProcessedViewsCount { get; set; }
        public int CopiedFilterInstances { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
