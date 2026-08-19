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
            List<SourceViewItem> list = new List<SourceViewItem>();

            List<View> allViews = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && IsValidViewType(v.ViewType))
                .Where(v => v.GetFilters().Count > 0)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (View v in allViews)
            {
                list.Add(new SourceViewItem
                {
                    ViewId = v.Id,
                    ViewName = v.Name,
                    ViewType = v.ViewType,
                    IsTemplate = false,
                    FilterCount = v.GetFilters().Count
                });
            }

            List<View> templates = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate && IsValidViewType(v.ViewType))
                .Where(v => v.GetFilters().Count > 0)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (View t in templates)
            {
                list.Add(new SourceViewItem
                {
                    ViewId = t.Id,
                    ViewName = t.Name,
                    ViewType = t.ViewType,
                    IsTemplate = true,
                    FilterCount = t.GetFilters().Count
                });
            }

            return list;
        }

        public List<BccFilterCopyItem> GetViewFilters(ElementId sourceViewId)
        {
            List<BccFilterCopyItem> list = new List<BccFilterCopyItem>();
            if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId) return list;

            View sourceView = _doc.GetElement(sourceViewId) as View;
            if (sourceView == null) return list;

            ICollection<ElementId> filterIds = sourceView.GetFilters();
            foreach (ElementId fId in filterIds)
            {
                ParameterFilterElement filterElem = _doc.GetElement(fId) as ParameterFilterElement;
                if (filterElem == null) continue;

                bool isVisible = sourceView.GetFilterVisibility(fId);
                OverrideGraphicSettings overrides = sourceView.GetFilterOverrides(fId);
                bool isHalftone = overrides != null ? overrides.Halftone : false;
                string summary = BuildOverridesSummary(overrides, isVisible, isHalftone);

                list.Add(new BccFilterCopyItem
                {
                    FilterId = fId,
                    FilterName = filterElem.Name,
                    IsVisible = isVisible,
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
                .Where(v => v.Id != excludeSourceId && IsValidViewType(v.ViewType))
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
            if (sourceViewId == null || selectedFilters == null || selectedFilters.Count == 0 || selectedTargets == null || selectedTargets.Count == 0)
            {
                result.Success = false;
                result.Message = "Не вырезаны фильтры или целевые виды.";
                return result;
            }

            using (Transaction tx = new Transaction(_doc, "BIMBCC — Копирование фильтров видов"))
            {
                tx.Start();

                HashSet<ElementId> processedViews = new HashSet<ElementId>();

                foreach (BccTargetViewItem targetItem in selectedTargets)
                {
                    if (!targetItem.IsSelected) continue;

                    View targetView = _doc.GetElement(targetItem.ViewId) as View;
                    if (targetView == null) continue;

                    // Если у вида применен шаблон и включена опция применяться к шаблону
                    List<View> viewsToApply = new List<View>();
                    viewsToApply.Add(targetView);

                    if (targetItem.HasTemplateApplied && !targetItem.IsTemplate && options.CopyToAppliedTemplates)
                    {
                        View templateView = _doc.GetElement(targetItem.TemplateId) as View;
                        if (templateView != null && processedViews.Add(templateView.Id))
                        {
                            viewsToApply.Add(templateView);
                        }
                    }

                    foreach (View v in viewsToApply)
                    {
                        if (v == null) continue;
                        ICollection<ElementId> existingFilters = v.GetFilters();

                        foreach (BccFilterCopyItem BccFilterCopyItem in selectedFilters)
                        {
                            if (!BccFilterCopyItem.IsSelected) continue;

                            ElementId fId = BccFilterCopyItem.FilterId;
                            bool filterExists = existingFilters.Contains(fId);

                            if (filterExists && !options.OverwriteExistingFilters)
                            {
                                result.SkippedCount++;
                                continue;
                            }

                            try
                            {
                                if (!filterExists)
                                {
                                    v.AddFilter(fId);
                                }

                                if (options.CopyVisibility)
                                {
                                    v.SetFilterVisibility(fId, BccFilterCopyItem.IsVisible);
                                }

                                if (options.CopyGraphicOverrides && BccFilterCopyItem.GraphicOverrides != null)
                                {
                                    OverrideGraphicSettings ogs = BccFilterCopyItem.GraphicOverrides;
                                    if (!options.CopyHalftone)
                                    {
                                        OverrideGraphicSettings currentTargetOgs = v.GetFilterOverrides(fId);
                                        bool currentHalftone = currentTargetOgs != null ? currentTargetOgs.Halftone : false;
                                        ogs.SetHalftone(currentHalftone);
                                    }
                                    v.SetFilterOverrides(fId, ogs);
                                }

                                result.CopiedFilterInstances++;
                            }
                            catch (Exception ex)
                            {
                                result.Warnings.Add($"Вид [{v.Name}]: Ошибка фильтра [{BccFilterCopyItem.FilterName}]: {ex.Message}");
                            }
                        }
                    }

                    result.ProcessedViewsCount++;
                }

                tx.Commit();
            }

            result.Success = true;
            result.Message = $"Успешно скопировано {result.CopiedFilterInstances} фильтров на {result.ProcessedViewsCount} видов/шаблонов.";
            return result;
        }

        private static bool IsValidViewType(ViewType vt)
        {
            return vt == ViewType.FloorPlan ||
                   vt == ViewType.CeilingPlan ||
                   vt == ViewType.ThreeD ||
                   vt == ViewType.Section ||
                   vt == ViewType.Elevation ||
                   vt == ViewType.EngineeringPlan ||
                   vt == ViewType.AreaPlan ||
                   vt == ViewType.DraftingView;
        }

        private static string BuildOverridesSummary(OverrideGraphicSettings ogs, bool isVis, bool isHalftone)
        {
            if (ogs == null) return "Без переопределений";

            List<string> parts = new List<string>();
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

            return parts.Count > 0 ? string.Join(", ", parts) : "По умолчанию";
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
