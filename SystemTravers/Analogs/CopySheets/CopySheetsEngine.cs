using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Analogs.CopySheets
{
    public enum ConflictResolution
    {
        AddSuffix,
        Skip,
        OverwriteParameters
    }

    public class CopySheetsOptions
    {
        public bool CopySheetParameters { get; set; } = true;
        public bool CopyTitleBlock { get; set; } = true;
        public bool CopyDraftingViews { get; set; } = true;
        public bool CopyLegendViews { get; set; } = true;
        public bool CopySchedules { get; set; } = true;
        public ConflictResolution ConflictMode { get; set; } = ConflictResolution.AddSuffix;
        public string Suffix { get; set; } = "_Копия";
    }

    public class CopySheetsResult
    {
        public int SheetsCreated { get; set; }
        public int SheetsSkipped { get; set; }
        public int ViewsCopied { get; set; }
        public int SchedulesPlaced { get; set; }
        public List<string> Messages { get; } = new List<string>();
    }

    public static class CopySheetsEngine
    {
        public class DuplicateFailurePreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (FailureMessageAccessor msg in failuresAccessor.GetFailureMessages())
                {
                    if (msg.GetFailureDefinitionId() == BuiltInFailures.CopyPasteFailures.CannotCopyDuplicates)
                    {
                        failuresAccessor.DeleteWarning(msg);
                    }
                }
                return FailureProcessingResult.Continue;
            }
        }

        public static CopySheetsResult CopySheets(
            Document sourceDoc,
            List<ViewSheet> sourceSheets,
            Document targetDoc,
            CopySheetsOptions options)
        {
            var result = new CopySheetsResult();
            if (sourceDoc == null || targetDoc == null || sourceSheets == null || sourceSheets.Count == 0)
            {
                return result;
            }

            var copyPasteOptions = new CopyPasteOptions();
            copyPasteOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

            using (Transaction tr = new Transaction(targetDoc, "Копирование листов из документа"))
            {
                FailureHandlingOptions fho = tr.GetFailureHandlingOptions();
                fho.SetFailuresPreprocessor(new DuplicateFailurePreprocessor());
                tr.SetFailureHandlingOptions(fho);

                tr.Start();

                // Получаем все существующие номера листов в целевом документе
                var existingSheetNumbers = new HashSet<string>(
                    new FilteredElementCollector(targetDoc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Select(s => s.SheetNumber),
                    StringComparer.OrdinalIgnoreCase);

                foreach (ViewSheet srcSheet in sourceSheets)
                {
                    string targetNumber = srcSheet.SheetNumber;
                    string targetName = srcSheet.Name;

                    // Разрешение конфликтов номеров
                    if (existingSheetNumbers.Contains(targetNumber))
                    {
                        if (options.ConflictMode == ConflictResolution.Skip)
                        {
                            result.SheetsSkipped++;
                            result.Messages.Add($"Лист {targetNumber} пропущен (уже существует).");
                            continue;
                        }
                        else if (options.ConflictMode == ConflictResolution.AddSuffix)
                        {
                            string newNum = targetNumber + options.Suffix;
                            int counter = 1;
                            while (existingSheetNumbers.Contains(newNum))
                            {
                                newNum = $"{targetNumber}{options.Suffix}_{counter}";
                                counter++;
                            }
                            targetNumber = newNum;
                        }
                    }

                    // 1. Поиск и перенос основной надписи (TitleBlock)
                    FamilyInstance srcTitleBlock = new FilteredElementCollector(sourceDoc, srcSheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .FirstOrDefault();

                    ElementId targetTitleBlockSymbolId = ElementId.InvalidElementId;
                    if (options.CopyTitleBlock && srcTitleBlock != null && srcTitleBlock.Symbol != null)
                    {
                        targetTitleBlockSymbolId = GetOrCreateTitleBlockSymbol(sourceDoc, srcTitleBlock.Symbol, targetDoc, copyPasteOptions);
                    }

                    // 2. Создание листа в целевом документе
                    ViewSheet newSheet = null;
                    try
                    {
                        newSheet = ViewSheet.Create(targetDoc, targetTitleBlockSymbolId);
                        newSheet.SheetNumber = targetNumber;
                        newSheet.Name = targetName;
                        existingSheetNumbers.Add(targetNumber);
                        result.SheetsCreated++;
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"Ошибка создания листа {targetNumber}: {ex.Message}");
                        continue;
                    }

                    // 3. Перенос параметров листа
                    if (options.CopySheetParameters)
                    {
                        CopyElementParameters(srcSheet, newSheet);
                    }

                    // 4. Перенос параметров основной надписи
                    if (options.CopyTitleBlock && srcTitleBlock != null)
                    {
                        FamilyInstance newTitleBlock = new FilteredElementCollector(targetDoc, newSheet.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsNotElementType()
                            .Cast<FamilyInstance>()
                            .FirstOrDefault();

                        if (newTitleBlock != null)
                        {
                            CopyElementParameters(srcTitleBlock, newTitleBlock);
                        }
                    }

                    // 5. Перенос чертежных видов и легенд
                    if (options.CopyDraftingViews || options.CopyLegendViews)
                    {
                        CopyPlacedViews(sourceDoc, srcSheet, targetDoc, newSheet, options, copyPasteOptions, result);
                    }

                    // 6. Перенос спецификаций на листе
                    if (options.CopySchedules)
                    {
                        CopyPlacedSchedules(sourceDoc, srcSheet, targetDoc, newSheet, copyPasteOptions, result);
                    }
                }

                tr.Commit();
            }

            return result;
        }

        private static ElementId GetOrCreateTitleBlockSymbol(
            Document sourceDoc,
            FamilySymbol srcSymbol,
            Document targetDoc,
            CopyPasteOptions copyOptions)
        {
            if (srcSymbol == null) return ElementId.InvalidElementId;

            // Ищем совпадение в целевом документе по имени семейства и типоразмера
            FamilySymbol targetSymbol = new FilteredElementCollector(targetDoc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Family.Name.Equals(srcSymbol.Family.Name, StringComparison.OrdinalIgnoreCase) &&
                                      fs.Name.Equals(srcSymbol.Name, StringComparison.OrdinalIgnoreCase));

            if (targetSymbol != null) return targetSymbol.Id;

            // Если не найден, копируем семейство в целевой документ
            try
            {
                var copiedIds = ElementTransformUtils.CopyElements(
                    sourceDoc,
                    new List<ElementId> { srcSymbol.Id },
                    targetDoc,
                    Transform.Identity,
                    copyOptions);

                if (copiedIds != null && copiedIds.Count > 0)
                {
                    return copiedIds.First();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Не удалось скопировать основную надпись {srcSymbol.Name}", ex);
            }

            return ElementId.InvalidElementId;
        }

        private static void CopyPlacedViews(
            Document sourceDoc,
            ViewSheet srcSheet,
            Document targetDoc,
            ViewSheet newSheet,
            CopySheetsOptions options,
            CopyPasteOptions copyOptions,
            CopySheetsResult result)
        {
            var viewports = new FilteredElementCollector(sourceDoc, srcSheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            foreach (Viewport vp in viewports)
            {
                View placedView = sourceDoc.GetElement(vp.ViewId) as View;
                if (placedView == null) continue;

                XYZ center = vp.GetBoxCenter();

                if (placedView.ViewType == ViewType.DraftingView && options.CopyDraftingViews)
                {
                    try
                    {
                        var copiedViewIds = ElementTransformUtils.CopyElements(
                            sourceDoc,
                            new List<ElementId> { placedView.Id },
                            targetDoc,
                            Transform.Identity,
                            copyOptions);

                        if (copiedViewIds != null && copiedViewIds.Count > 0)
                        {
                            ElementId newViewId = copiedViewIds.First();
                            Viewport.Create(targetDoc, newSheet.Id, newViewId, center);
                            result.ViewsCopied++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"Не удалось скопировать чертежный вид {placedView.Name}: {ex.Message}");
                    }
                }
                else if (placedView.ViewType == ViewType.Legend && options.CopyLegendViews)
                {
                    try
                    {
                        // Ищем легенду в целевом документе по имени
                        View targetLegend = new FilteredElementCollector(targetDoc)
                            .OfClass(typeof(View))
                            .Cast<View>()
                            .FirstOrDefault(v => v.ViewType == ViewType.Legend && v.Name.Equals(placedView.Name, StringComparison.OrdinalIgnoreCase));

                        if (targetLegend == null)
                        {
                            var copiedIds = ElementTransformUtils.CopyElements(
                                sourceDoc,
                                new List<ElementId> { placedView.Id },
                                targetDoc,
                                Transform.Identity,
                                copyOptions);

                            if (copiedIds != null && copiedIds.Count > 0)
                            {
                                targetLegend = targetDoc.GetElement(copiedIds.First()) as View;
                            }
                        }

                        if (targetLegend != null)
                        {
                            Viewport.Create(targetDoc, newSheet.Id, targetLegend.Id, center);
                            result.ViewsCopied++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"Не удалось скопировать легенду {placedView.Name}: {ex.Message}");
                    }
                }
            }
        }

        private static void CopyPlacedSchedules(
            Document sourceDoc,
            ViewSheet srcSheet,
            Document targetDoc,
            ViewSheet newSheet,
            CopyPasteOptions copyOptions,
            CopySheetsResult result)
        {
            var scheduleInstances = new FilteredElementCollector(sourceDoc, srcSheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            foreach (var ssi in scheduleInstances)
            {
                if (ssi.IsTitleblockRevisionSchedule) continue;

                ViewSchedule srcSched = sourceDoc.GetElement(ssi.ScheduleId) as ViewSchedule;
                if (srcSched == null || srcSched.IsTemplate) continue;

                try
                {
                    // Ищем спецификацию в целевом документе по имени
                    ViewSchedule targetSched = new FilteredElementCollector(targetDoc)
                        .OfClass(typeof(ViewSchedule))
                        .Cast<ViewSchedule>()
                        .FirstOrDefault(s => !s.IsTemplate && s.Name.Equals(srcSched.Name, StringComparison.OrdinalIgnoreCase));

                    if (targetSched == null)
                    {
                        var copiedIds = ElementTransformUtils.CopyElements(
                            sourceDoc,
                            new List<ElementId> { srcSched.Id },
                            targetDoc,
                            Transform.Identity,
                            copyOptions);

                        if (copiedIds != null && copiedIds.Count > 0)
                        {
                            targetSched = targetDoc.GetElement(copiedIds.First()) as ViewSchedule;
                        }
                    }

                    if (targetSched != null)
                    {
                        ScheduleSheetInstance.Create(targetDoc, newSheet.Id, targetSched.Id, ssi.Point);
                        result.SchedulesPlaced++;
                    }
                }
                catch (Exception ex)
                {
                    result.Messages.Add($"Не удалось скопировать спецификацию {srcSched.Name}: {ex.Message}");
                }
            }
        }

        private static void CopyElementParameters(Element source, Element target)
        {
            if (source == null || target == null) return;

            foreach (Parameter srcParam in source.Parameters)
            {
                if (srcParam.IsReadOnly || !srcParam.HasValue) continue;

                // Пропускаем уникальные идентификаторы и номера, которые задаются при создании
                if (srcParam.Definition.Name == "Номер листа" ||
                    srcParam.Definition.Name == "Sheet Number" ||
                    srcParam.Definition.Name == "Имя листа" ||
                    srcParam.Definition.Name == "Sheet Name")
                {
                    continue;
                }

                Parameter targetParam = target.LookupParameter(srcParam.Definition.Name);
                if (targetParam != null && !targetParam.IsReadOnly)
                {
                    try
                    {
                        switch (srcParam.StorageType)
                        {
                            case StorageType.String:
                                targetParam.Set(srcParam.AsString());
                                break;
                            case StorageType.Double:
                                targetParam.Set(srcParam.AsDouble());
                                break;
                            case StorageType.Integer:
                                targetParam.Set(srcParam.AsInteger());
                                break;
                        }
                    }
                    catch { }
                }
            }
        }

        private class DuplicateTypeHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
