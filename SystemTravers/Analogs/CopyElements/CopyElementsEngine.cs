using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Analogs.CopyElements
{
    public enum ElementDuplicateAction
    {
        UseDestinationTypes,
        CreateNewTypes
    }

    public class CopyElementsOptions
    {
        public ElementDuplicateAction DuplicateAction { get; set; } = ElementDuplicateAction.UseDestinationTypes;
        public bool SuppressWarnings { get; set; } = true;
        public bool CloseGeneratedViews { get; set; } = true;
        public Transform Transform { get; set; } = Transform.Identity;
    }

    public class CopyElementsResult
    {
        public int CopiedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Messages { get; } = new List<string>();
    }

    public class SafeDuplicateHandler : IDuplicateTypeNamesHandler
    {
        private readonly ElementDuplicateAction _action;
        public SafeDuplicateHandler(ElementDuplicateAction action)
        {
            _action = action;
        }

        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            if (_action == ElementDuplicateAction.UseDestinationTypes)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
            return DuplicateTypeAction.Abort;
        }
    }

    public class WarningSuppressor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (FailureMessageAccessor msg in failuresAccessor.GetFailureMessages())
            {
                if (msg.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(msg);
                }
            }
            return FailureProcessingResult.Continue;
        }
    }

    public static class CopyElementsEngine
    {
        public static CopyElementsResult CopyElements(
            Document sourceDoc,
            ICollection<ElementId> elementIds,
            Document targetDoc,
            CopyElementsOptions options)
        {
            var result = new CopyElementsResult();
            if (sourceDoc == null || targetDoc == null || elementIds == null || elementIds.Count == 0)
            {
                return result;
            }

            var copyPasteOptions = new CopyPasteOptions();
            copyPasteOptions.SetDuplicateTypeNamesHandler(new SafeDuplicateHandler(options.DuplicateAction));

            using (Transaction tr = new Transaction(targetDoc, "Копирование элементов между проектами"))
            {
                if (options.SuppressWarnings)
                {
                    FailureHandlingOptions fho = tr.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new WarningSuppressor());
                    fho.SetClearAfterRollback(true);
                    tr.SetFailureHandlingOptions(fho);
                }

                tr.Start();

                try
                {
                    Transform t = options.Transform ?? Transform.Identity;
                    ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(
                        sourceDoc,
                        elementIds,
                        targetDoc,
                        t,
                        copyPasteOptions);

                    if (copiedIds != null)
                    {
                        result.CopiedCount = copiedIds.Count;
                    }

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    if (tr.HasStarted())
                    {
                        tr.RollBack();
                    }
                    result.Messages.Add("Ошибка транзакции копирования: " + ex.Message);
                }
            }

            return result;
        }

        public static List<Element> GetProjectStandards(Document doc)
        {
            var list = new List<Element>();
            if (doc == null) return list;

            // 1. Шаблоны видов
            var viewTemplates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .ToList();
            list.AddRange(viewTemplates);

            // 2. Фильтры видов
            var filters = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .ToList();
            list.AddRange(filters);

            // 3. Спецификации
            var schedules = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && !s.IsInternalKeynoteSchedule && !s.IsTitleblockRevisionSchedule)
                .ToList();
            list.AddRange(schedules);

            // 4. Материалы
            var materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .ToList();
            list.AddRange(materials);

            // 5. Текстовые стили
            var textTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .ToList();
            list.AddRange(textTypes);

            // 6. Размерные типы
            var dimTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .ToList();
            list.AddRange(dimTypes);

            // 7. Системные типоразмеры стен, перекрытий, воздуховодов, труб
            var systemTypes = new FilteredElementCollector(doc)
                .WherePasses(new ElementIsElementTypeFilter())
                .Where(e =>
                {
                    if (e is WallType || e is FloorType || e is RoofType || e is CeilingType) return true;
                    string catName = e.Category?.Name ?? "";
                    if (catName.IndexOf("воздуховод", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        catName.IndexOf("труб", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        catName.IndexOf("кабельн", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                    return false;
                })
                .ToList();
            list.AddRange(systemTypes);

            return list;
        }
    }
}
