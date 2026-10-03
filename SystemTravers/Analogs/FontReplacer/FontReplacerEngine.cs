using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Analogs.FontReplacer
{
    public class FontReplacementSettings
    {
        public string TargetFontName { get; set; } = "Arial";

        public bool ChangeWidthScale { get; set; } = false;
        public double WidthScale { get; set; } = 1.0;

        public bool ChangeSize { get; set; } = false;
        public double TextSizeMm { get; set; } = 2.5;

        public bool ChangeBold { get; set; } = false;
        public bool IsBold { get; set; } = false;

        public bool ChangeItalic { get; set; } = false;
        public bool IsItalic { get; set; } = false;

        public bool ChangeUnderline { get; set; } = false;
        public bool IsUnderline { get; set; } = false;

        public bool ChangeBackground { get; set; } = false;
        public bool IsTransparent { get; set; } = true;

        public bool ChangeColor { get; set; } = false;
        public int ColorRgb { get; set; } = 0; // 0 = Black

        public bool RenameStyles { get; set; } = false;
        public string FindTextInName { get; set; } = "";
        public string ReplaceTextInName { get; set; } = "";
    }

    public class FontReplacerEngine
    {
        public class ReplacementStats
        {
            public int TextNoteTypesUpdated { get; set; }
            public int DimensionTypesUpdated { get; set; }
            public int SchedulesUpdated { get; set; }
            public int FamiliesUpdated { get; set; }
            public List<string> Warnings { get; } = new List<string>();
        }

        public static ReplacementStats ExecuteReplacement(
            Document doc,
            FontReplacementSettings settings,
            List<ElementId> selectedTextTypeIds,
            List<ElementId> selectedDimTypeIds,
            List<ElementId> selectedScheduleIds,
            List<ElementId> selectedFamilyIds)
        {
            var stats = new ReplacementStats();

            // 1. Текстовые стили (TextNoteType)
            if (selectedTextTypeIds != null && selectedTextTypeIds.Count > 0)
            {
                using (Transaction tr = new Transaction(doc, "Замена шрифта в текстовых стилях"))
                {
                    tr.Start();
                    foreach (ElementId id in selectedTextTypeIds)
                    {
                        if (doc.GetElement(id) is TextNoteType tnt)
                        {
                            if (ApplySettingsToType(tnt, settings))
                            {
                                stats.TextNoteTypesUpdated++;
                            }
                        }
                    }
                    tr.Commit();
                }
            }

            // 2. Размерные стили (DimensionType)
            if (selectedDimTypeIds != null && selectedDimTypeIds.Count > 0)
            {
                using (Transaction tr = new Transaction(doc, "Замена шрифта в размерных стилях"))
                {
                    tr.Start();
                    foreach (ElementId id in selectedDimTypeIds)
                    {
                        if (doc.GetElement(id) is DimensionType dt)
                        {
                            if (ApplySettingsToType(dt, settings))
                            {
                                stats.DimensionTypesUpdated++;
                            }
                        }
                    }
                    tr.Commit();
                }
            }

            // 3. Спецификации (ViewSchedule)
            if (selectedScheduleIds != null && selectedScheduleIds.Count > 0)
            {
                using (Transaction tr = new Transaction(doc, "Замена шрифта в спецификациях"))
                {
                    tr.Start();
                    foreach (ElementId id in selectedScheduleIds)
                    {
                        if (doc.GetElement(id) is ViewSchedule vs)
                        {
                            bool scheduleModified = false;

                            string[] schedParamNames = new string[]
                            {
                                "Текст заголовка", "Title text",
                                "Текст заголовков граф", "Header text",
                                "Текст данных", "Body text"
                            };

                            foreach (string pName in schedParamNames)
                            {
                                Parameter p = vs.LookupParameter(pName);
                                if (p != null && p.HasValue)
                                {
                                    ElementId typeId = p.AsElementId();
                                    if (typeId != null && typeId != ElementId.InvalidElementId)
                                    {
                                        if (doc.GetElement(typeId) is TextNoteType schedTnt)
                                        {
                                            if (ApplySettingsToType(schedTnt, settings))
                                            {
                                                scheduleModified = true;
                                            }
                                        }
                                    }
                                }
                            }

                            if (scheduleModified) stats.SchedulesUpdated++;
                        }
                    }
                    tr.Commit();
                }
            }

            // 4. Семейства аннотаций, марок и штампов (Family)
            if (selectedFamilyIds != null && selectedFamilyIds.Count > 0)
            {
                var loadOpts = new CustomFamilyLoadOptions();

                foreach (ElementId fid in selectedFamilyIds)
                {
                    if (doc.GetElement(fid) is Family fam && fam.IsEditable)
                    {
                        Document famDoc = null;
                        try
                        {
                            famDoc = doc.EditFamily(fam);
                            if (famDoc != null)
                            {
                                bool famModified = false;
                                using (Transaction famTr = new Transaction(famDoc, "Замена шрифта в семействе"))
                                {
                                    famTr.Start();

                                    // Обновляем TextNoteType внутри семейства
                                    var famTextTypes = new FilteredElementCollector(famDoc)
                                        .OfClass(typeof(TextNoteType))
                                        .Cast<TextNoteType>()
                                        .ToList();

                                    foreach (var ftt in famTextTypes)
                                    {
                                        if (ApplySettingsToType(ftt, settings))
                                        {
                                            famModified = true;
                                        }
                                    }

                                    // Обновляем метки (TextElement / TextNote)
                                    var famNotes = new FilteredElementCollector(famDoc)
                                        .OfClass(typeof(TextElement))
                                        .Cast<TextElement>()
                                        .ToList();

                                    foreach (var fn in famNotes)
                                    {
                                        ElementId typeId = fn.GetTypeId();
                                        if (typeId != null && famDoc.GetElement(typeId) is TextNoteType tnt)
                                        {
                                            if (ApplySettingsToType(tnt, settings))
                                            {
                                                famModified = true;
                                            }
                                        }
                                    }

                                    famTr.Commit();
                                }

                                if (famModified)
                                {
                                    famDoc.LoadFamily(doc, loadOpts);
                                    stats.FamiliesUpdated++;
                                }

                                famDoc.Close(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            stats.Warnings.Add($"Семейство '{fam.Name}': {ex.Message}");
                            try { famDoc?.Close(false); } catch { }
                        }
                    }
                }
            }

            return stats;
        }

        private static bool ApplySettingsToType(ElementType typeElem, FontReplacementSettings settings)
        {
            if (typeElem == null) return false;
            bool modified = false;

            try
            {
                // 1. Шрифт (Гарнитура)
                Parameter pFont = typeElem.get_Parameter(BuiltInParameter.TEXT_FONT);
                if (pFont != null && !pFont.IsReadOnly)
                {
                    if (!string.Equals(pFont.AsString(), settings.TargetFontName, StringComparison.OrdinalIgnoreCase))
                    {
                        pFont.Set(settings.TargetFontName);
                        modified = true;
                    }
                }

                // 2. Коэффициент сжатия
                if (settings.ChangeWidthScale)
                {
                    Parameter pWidth = typeElem.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE);
                    if (pWidth != null && !pWidth.IsReadOnly && settings.WidthScale > 0.05)
                    {
                        pWidth.Set(settings.WidthScale);
                        modified = true;
                    }
                }

                // 3. Размер шрифта (мм -> фут)
                if (settings.ChangeSize && settings.TextSizeMm > 0.1)
                {
                    Parameter pSize = typeElem.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (pSize != null && !pSize.IsReadOnly)
                    {
                        pSize.Set(settings.TextSizeMm / 304.8);
                        modified = true;
                    }
                }

                // 4. Начертание: Жирный
                if (settings.ChangeBold)
                {
                    Parameter pBold = typeElem.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD);
                    if (pBold != null && !pBold.IsReadOnly)
                    {
                        pBold.Set(settings.IsBold ? 1 : 0);
                        modified = true;
                    }
                }

                // 5. Начертание: Курсив
                if (settings.ChangeItalic)
                {
                    Parameter pItalic = typeElem.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC);
                    if (pItalic != null && !pItalic.IsReadOnly)
                    {
                        pItalic.Set(settings.IsItalic ? 1 : 0);
                        modified = true;
                    }
                }

                // 6. Подчеркнутый
                if (settings.ChangeUnderline)
                {
                    Parameter pUnderline = typeElem.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE);
                    if (pUnderline != null && !pUnderline.IsReadOnly)
                    {
                        pUnderline.Set(settings.IsUnderline ? 1 : 0);
                        modified = true;
                    }
                }

                // 7. Подложка (Фон: Прозрачный / Непрозрачный)
                if (settings.ChangeBackground)
                {
                    Parameter pBg = typeElem.get_Parameter(BuiltInParameter.TEXT_BACKGROUND);
                    if (pBg != null && !pBg.IsReadOnly)
                    {
                        pBg.Set(settings.IsTransparent ? 1 : 0);
                        modified = true;
                    }
                }

                // 8. Цвет
                if (settings.ChangeColor)
                {
                    Parameter pColor = typeElem.get_Parameter(BuiltInParameter.LINE_COLOR);
                    if (pColor != null && !pColor.IsReadOnly)
                    {
                        pColor.Set(settings.ColorRgb);
                        modified = true;
                    }
                }

                // 9. Переименование стиля
                if (settings.RenameStyles && !string.IsNullOrEmpty(settings.FindTextInName))
                {
                    string oldName = typeElem.Name;
                    if (oldName.Contains(settings.FindTextInName))
                    {
                        string newName = oldName.Replace(settings.FindTextInName, settings.ReplaceTextInName);
                        if (!string.IsNullOrWhiteSpace(newName) && newName != oldName)
                        {
                            try
                            {
                                typeElem.Name = newName;
                                modified = true;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка изменения свойств шрифта в типе {typeElem.Name}", ex);
            }

            return modified;
        }

        public class CustomFamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }
    }
}
