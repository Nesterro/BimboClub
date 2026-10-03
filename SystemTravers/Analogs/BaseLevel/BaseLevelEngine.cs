using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Analogs.BaseLevel
{
    public static class BaseLevelEngine
    {
        public static Level FindTargetLevel(List<Level> levels, double elemZ, LevelChoiceMode mode, long specificId, Document doc)
        {
            if (levels == null || levels.Count == 0) return null;

            switch (mode)
            {
                case LevelChoiceMode.Specific:
                    if (specificId > 0)
                    {
#if NET8_0_OR_GREATER
                        ElementId specId = new ElementId(specificId);
#else
                        ElementId specId = new ElementId((int)specificId);
#endif
                        return doc.GetElement(specId) as Level;
                    }
                    return null;

                case LevelChoiceMode.Nearest:
                    return levels.OrderBy(l => Math.Abs(l.Elevation - elemZ)).FirstOrDefault();

                case LevelChoiceMode.NearestBelow:
                    // Самый высокий уровень, отметка которого ниже или равна отметке элемента (с допуском 2 мм)
                    Level below = levels.Where(l => l.Elevation <= elemZ + (2.0 / 304.8))
                                        .OrderByDescending(l => l.Elevation)
                                        .FirstOrDefault();
                    return below ?? levels.OrderBy(l => l.Elevation).FirstOrDefault();

                case LevelChoiceMode.NearestAbove:
                    // Самый низкий уровень, отметка которого выше или равна отметке элемента
                    Level above = levels.Where(l => l.Elevation >= elemZ - (2.0 / 304.8))
                                        .OrderBy(l => l.Elevation)
                                        .FirstOrDefault();
                    return above ?? levels.OrderByDescending(l => l.Elevation).FirstOrDefault();

                default:
                    return null;
            }
        }

        public static bool ApplyRuleToElement(Document doc, Element elem, BaseLevelRule rule, List<Level> levels)
        {
            if (elem == null || elem.Pinned) return false;

            BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
            if (bbox == null) return false;

            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;

            Level targetBase = null;
            if (rule.BaseMode != LevelChoiceMode.Keep)
            {
                targetBase = FindTargetLevel(levels, zMin, rule.BaseMode, rule.SpecificBaseLevelId, doc);
            }

            Level targetTop = null;
            if (rule.TopMode != LevelChoiceMode.Keep && rule.TopMode != LevelChoiceMode.Unbind)
            {
                targetTop = FindTargetLevel(levels, zMax, rule.TopMode, rule.SpecificTopLevelId, doc);
            }

            bool changed = false;

            try
            {
                // 1. СТЕНЫ
                if (elem is Wall wall)
                {
                    // Базовый уровень
                    if (targetBase != null)
                    {
                        Parameter pBase = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                        Parameter pBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET);

                        if (pBase != null && !pBase.IsReadOnly)
                        {
                            double newOffset = zMin - targetBase.Elevation;
                            pBase.Set(targetBase.Id);
                            if (pBaseOffset != null && !pBaseOffset.IsReadOnly)
                            {
                                pBaseOffset.Set(newOffset);
                            }
                            changed = true;
                        }
                    }

                    // Верхний уровень
                    if (rule.TopMode == LevelChoiceMode.Unbind)
                    {
                        Parameter pTop = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                        Parameter pUserHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);

                        if (pTop != null && !pTop.IsReadOnly)
                        {
                            double height = zMax - zMin;
                            pTop.Set(ElementId.InvalidElementId);
                            if (pUserHeight != null && !pUserHeight.IsReadOnly && height > 0.01)
                            {
                                pUserHeight.Set(height);
                            }
                            changed = true;
                        }
                    }
                    else if (targetTop != null)
                    {
                        Parameter pTop = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                        Parameter pTopOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);

                        if (pTop != null && !pTop.IsReadOnly)
                        {
                            double newOffset = zMax - targetTop.Elevation;
                            pTop.Set(targetTop.Id);
                            if (pTopOffset != null && !pTopOffset.IsReadOnly)
                            {
                                pTopOffset.Set(newOffset);
                            }
                            changed = true;
                        }
                    }

                    return changed;
                }

                // 2. КОЛОННЫ (Несущие или архитектурные)
                Parameter colBaseParam = elem.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                if (colBaseParam != null && !colBaseParam.IsReadOnly)
                {
                    if (targetBase != null)
                    {
                        Parameter pOffset = elem.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                        double newOffset = zMin - targetBase.Elevation;
                        colBaseParam.Set(targetBase.Id);
                        if (pOffset != null && !pOffset.IsReadOnly)
                        {
                            pOffset.Set(newOffset);
                        }
                        changed = true;
                    }

                    Parameter colTopParam = elem.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (colTopParam != null && !colTopParam.IsReadOnly)
                    {
                        if (rule.TopMode == LevelChoiceMode.Unbind)
                        {
                            colTopParam.Set(ElementId.InvalidElementId);
                            changed = true;
                        }
                        else if (targetTop != null)
                        {
                            Parameter pTopOffset = elem.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            double newOffset = zMax - targetTop.Elevation;
                            colTopParam.Set(targetTop.Id);
                            if (pTopOffset != null && !pTopOffset.IsReadOnly)
                            {
                                pTopOffset.Set(newOffset);
                            }
                            changed = true;
                        }
                    }

                    return changed;
                }

                // 3. ПЕРЕКРЫТИЯ (Floors)
                if (elem is Floor floor)
                {
                    if (targetBase != null)
                    {
                        Parameter pLevel = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                        Parameter pOffset = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);

                        if (pLevel != null && !pLevel.IsReadOnly)
                        {
                            double newOffset = zMin - targetBase.Elevation;
                            pLevel.Set(targetBase.Id);
                            if (pOffset != null && !pOffset.IsReadOnly)
                            {
                                pOffset.Set(newOffset);
                            }
                            changed = true;
                        }
                    }
                    return changed;
                }

                // 4. КРЫШИ (Roofs)
                if (elem is RoofBase roof)
                {
                    if (targetBase != null)
                    {
                        Parameter pLevel = roof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);
                        Parameter pOffset = roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM);

                        if (pLevel != null && !pLevel.IsReadOnly)
                        {
                            double newOffset = zMin - targetBase.Elevation;
                            pLevel.Set(targetBase.Id);
                            if (pOffset != null && !pOffset.IsReadOnly)
                            {
                                pOffset.Set(newOffset);
                            }
                            changed = true;
                        }
                    }
                    return changed;
                }

                // 5. MEP-КРИВЫЕ (Трубы, воздуховоды, кабельные лотки, короба)
                if (elem is MEPCurve mepCurve)
                {
                    if (targetBase != null)
                    {
                        Parameter pLevel = mepCurve.ReferenceLevel != null
                            ? mepCurve.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM)
                            : null;

                        if (pLevel == null)
                        {
                            pLevel = mepCurve.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                        }

                        if (pLevel != null && !pLevel.IsReadOnly)
                        {
                            // У MEP кривых геометрия зафиксирована в мировых координатах XYZ
                            pLevel.Set(targetBase.Id);
                            changed = true;
                        }
                    }
                    return changed;
                }

                // 6. БАЛКИ И НЕСУЩИЕ ЭЛЕМЕНТЫ КАРКАСА (Structural Framing)
                Parameter pFramingLevel = elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                if (pFramingLevel != null && !pFramingLevel.IsReadOnly && targetBase != null)
                {
                    pFramingLevel.Set(targetBase.Id);
                    changed = true;
                    return changed;
                }

                // 7. СТАНДАРТНЫЕ СЕМЕЙСТВА (FamilyInstance - оборудование, приборы, фитинги, мебель)
                if (elem is FamilyInstance fi)
                {
                    if (targetBase != null)
                    {
                        Parameter pLevel = fi.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                                           ?? fi.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                                           ?? fi.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM);

                        if (pLevel != null && !pLevel.IsReadOnly)
                        {
                            double newOffset = zMin - targetBase.Elevation;
                            pLevel.Set(targetBase.Id);

                            Parameter pOffset = fi.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                                                ?? fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);

                            if (pOffset != null && !pOffset.IsReadOnly)
                            {
                                pOffset.Set(newOffset);
                            }
                            changed = true;
                        }
                    }
                    return changed;
                }

                // 8. УНИВЕРСАЛЬНЫЙ ФОЛЛБЭК ДЛЯ ПРОЧИХ ЭЛЕМЕНТОВ
                if (targetBase != null)
                {
                    Parameter pLevel = elem.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                                       ?? elem.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM);

                    if (pLevel != null && !pLevel.IsReadOnly)
                    {
                        pLevel.Set(targetBase.Id);
                        Parameter pOffset = elem.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                        if (pOffset != null && !pOffset.IsReadOnly)
                        {
                            pOffset.Set(zMin - targetBase.Elevation);
                        }
                        changed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка смены уровня у элемента ID {elem.Id}: {ex.Message}", ex);
            }

            return changed;
        }

        public static bool ElementMatchesFilter(Element elem, BaseLevelRule rule)
        {
            if (elem == null) return false;

            // Проверка категории
            if (!string.IsNullOrEmpty(rule.CategoryName) && rule.CategoryName != "Все категории")
            {
                string catName = elem.Category?.Name;
                if (!string.Equals(catName, rule.CategoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Проверка параметра
            if (!string.IsNullOrEmpty(rule.ParameterName))
            {
                Parameter p = elem.LookupParameter(rule.ParameterName);
                if (p == null) return false;

                if (!string.IsNullOrEmpty(rule.ParameterValue))
                {
                    string pVal = p.AsValueString() ?? p.AsString() ?? "";
                    if (!pVal.IndexOf(rule.ParameterValue, StringComparison.OrdinalIgnoreCase).Equals(-1))
                    {
                        return true;
                    }
                    return false;
                }
            }

            return true;
        }
    }
}
