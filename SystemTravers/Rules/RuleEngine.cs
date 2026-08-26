using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BimboClub.Rules
{
    public class RuleExecutionResult
    {
        public int TotalRules { get; set; }
        public int ExecutedRules { get; set; }
        public int MatchedElementsCount { get; set; }
        public int UpdatedElementsCount { get; set; }
        public List<string> Messages { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class RuleEngine
    {
        private readonly Document _doc;
        private readonly UIDocument? _uiDoc;

        public RuleEngine(Document doc, UIDocument? uiDoc = null)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc;
        }

        public RuleExecutionResult Execute(RuleSet ruleSet, IProgress<double>? progress = null)
        {
            var result = new RuleExecutionResult();
            var activeRules = ruleSet.Rules.Where(r => r.IsEnabled).OrderBy(r => r.Index).ToList();
            result.TotalRules = activeRules.Count;

            if (activeRules.Count == 0)
            {
                result.Messages.Add("Нет активных правил для выполнения.");
                return result;
            }

            // 1. Collect candidate elements
            List<Element> candidates = CollectCandidateElements(ruleSet.Scope);
            if (candidates.Count == 0)
            {
                result.Messages.Add("Не найдено элементов для обработки в выбранной области.");
                return result;
            }

            using (Transaction trans = new Transaction(_doc, "BimboClub: Применение правил"))
            {
                trans.Start();

                var updatedElementIds = new HashSet<ElementId>();

                for (int i = 0; i < activeRules.Count; i++)
                {
                    var rule = activeRules[i];
                    double progressPercent = (double)(i + 1) / activeRules.Count * 100.0;
                    progress?.Report(progressPercent);

                    try
                    {
                        var matchingElements = candidates.Where(e => MatchesRuleFilters(e, rule.Filters)).ToList();
                        result.MatchedElementsCount += matchingElements.Count;

                        foreach (var elem in matchingElements)
                        {
                            string calculatedValue = CalculateRuleValue(elem, rule.Fragments);
                            if (string.IsNullOrEmpty(calculatedValue)) continue;

                            bool updated = SetTargetParameterValue(elem, rule.TargetParam, calculatedValue);
                            if (updated)
                            {
                                updatedElementIds.Add(elem.Id);
                            }
                        }

                        result.ExecutedRules++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Ошибка в правиле '{rule.Name}': {ex.Message}");
                    }
                }

                trans.Commit();
                result.UpdatedElementsCount = updatedElementIds.Count;
            }

            result.Messages.Add($"Успешно обновлено параметров у {result.UpdatedElementsCount} элементов.");
            return result;
        }

        private List<Element> CollectCandidateElements(RuleScope scope)
        {
            if (scope == RuleScope.Selection && _uiDoc != null)
            {
                var selIds = _uiDoc.Selection.GetElementIds();
                if (selIds.Count > 0)
                {
                    return selIds.Select(id => _doc.GetElement(id)).Where(e => e != null && e.IsValidObject).ToList();
                }
            }

            FilteredElementCollector collector;
            if (scope == RuleScope.ActiveView && _doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
            }
            else
            {
                collector = new FilteredElementCollector(_doc);
            }

            return collector
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                .ToList();
        }

        private bool MatchesRuleFilters(Element elem, IEnumerable<RuleFilter> filters)
        {
            foreach (var filter in filters)
            {
                if (!EvaluateFilter(elem, filter))
                {
                    return false;
                }
            }
            return true;
        }

        private bool EvaluateFilter(Element elem, RuleFilter filter)
        {
            bool matches = true;

            if (filter.FilterType == "ByCategories")
            {
                if (filter.Categories.Count == 0) return true;

                string? elemCatName = elem.Category?.Name;
                string? elemBuiltInCat = GetBuiltInCategoryName(elem.Category);

                matches = filter.Categories.Any(c => 
                    c.Equals(elemCatName, StringComparison.OrdinalIgnoreCase) || 
                    (elemBuiltInCat != null && c.Equals(elemBuiltInCat, StringComparison.OrdinalIgnoreCase)));
            }
            else if (filter.FilterType == "ElementIsElementType")
            {
                matches = elem is ElementType;
            }
            else if (filter.FilterType == "ByParameterValue")
            {
                string? actualValue = GetParameterStringValue(elem, filter.ParamId, ParamGettingMode.Self);
                matches = CompareValues(actualValue, filter.Value, filter.MatchType, filter.ParamId.StorageType);
            }
            else if (filter.FilterType == "ByInsulationParameterValue")
            {
                var insIds = InsulationLiningBase.GetInsulationIds(_doc, elem.Id);
                if (insIds.Count > 0)
                {
                    Element? insElem = _doc.GetElement(insIds.First());
                    if (insElem != null)
                    {
                        string? actualValue = GetParameterStringValue(insElem, filter.ParamId, ParamGettingMode.Self);
                        matches = CompareValues(actualValue, filter.Value, filter.MatchType, filter.ParamId.StorageType);
                    }
                    else
                    {
                        matches = false;
                    }
                }
                else
                {
                    // No insulation on element
                    matches = CompareValues(null, filter.Value, filter.MatchType, filter.ParamId.StorageType);
                }
            }

            return filter.Inverted ? !matches : matches;
        }

        private bool CompareValues(string? actual, string expected, FilterMatchType matchType, StorageType storageType)
        {
            actual ??= string.Empty;
            expected ??= string.Empty;

            if (matchType == FilterMatchType.IsEmpty) return string.IsNullOrWhiteSpace(actual);
            if (matchType == FilterMatchType.IsNotEmpty) return !string.IsNullOrWhiteSpace(actual);

            if (storageType == StorageType.Double && 
                double.TryParse(actual.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double actNum) &&
                double.TryParse(expected.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double expNum))
            {
                return matchType switch
                {
                    FilterMatchType.Equals => Math.Abs(actNum - expNum) < 0.0001,
                    FilterMatchType.NotEquals => Math.Abs(actNum - expNum) >= 0.0001,
                    FilterMatchType.GreaterThan => actNum > expNum,
                    FilterMatchType.GreaterThanOrEqual => actNum >= expNum,
                    FilterMatchType.LessThan => actNum < expNum,
                    FilterMatchType.LessThanOrEqual => actNum <= expNum,
                    _ => false
                };
            }

            return matchType switch
            {
                FilterMatchType.Equals => actual.Equals(expected, StringComparison.OrdinalIgnoreCase),
                FilterMatchType.NotEquals => !actual.Equals(expected, StringComparison.OrdinalIgnoreCase),
                FilterMatchType.Contains => actual.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0,
                FilterMatchType.NotContains => actual.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0,
                FilterMatchType.StartsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
                FilterMatchType.EndsWith => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private string CalculateRuleValue(Element elem, IEnumerable<RuleFragment> fragments)
        {
            var parts = new List<string>();

            foreach (var frag in fragments)
            {
                if (frag.FragmentType == "StaticText")
                {
                    parts.Add(frag.StaticText ?? string.Empty);
                }
                else if (frag.FragmentType == "Parameter")
                {
                    string? val = GetParameterStringValue(elem, frag.ParamId, frag.ParamMode, frag.ConvertToMillimeters, frag.RoundDecimal);
                    if (val != null) parts.Add(val);
                }
                else if (frag.FragmentType == "DuctThickness")
                {
                    string? val = CalculateDuctThickness(elem, frag.DuctThicknessData ?? DuctThicknessTable.CreateDefault());
                    if (val != null) parts.Add(val);
                }
                else if (frag.FragmentType == "Level")
                {
                    string? levelName = GetElementLevelName(elem);
                    if (levelName != null) parts.Add(levelName);
                }
            }

            return string.Concat(parts);
        }

        private string? CalculateDuctThickness(Element elem, DuctThicknessTable table)
        {
            // Try to extract Duct dimensions (Diameter or Width/Height)
            double widthMm = 0;
            double heightMm = 0;
            double diameterMm = 0;
            bool isRound = false;

            Parameter? diamParam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM) ?? elem.LookupParameter("Диаметр");
            if (diamParam != null && diamParam.HasValue)
            {
                diameterMm = diamParam.AsDouble() * 304.8;
                isRound = true;
            }
            else
            {
                Parameter? widthParam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM) ?? elem.LookupParameter("Ширина");
                Parameter? heightParam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM) ?? elem.LookupParameter("Высота");

                if (widthParam != null && widthParam.HasValue)
                {
                    widthMm = widthParam.AsDouble() * 304.8;
                }
                if (heightParam != null && heightParam.HasValue)
                {
                    heightMm = heightParam.AsDouble() * 304.8;
                }
            }

            if (isRound && diameterMm > 0)
            {
                foreach (var row in table.Round)
                {
                    if (diameterMm >= row.MinMm && diameterMm <= row.MaxMm)
                    {
                        return row.ThicknessMm.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
                    }
                }
            }
            else
            {
                double maxSideMm = Math.Max(widthMm, heightMm);
                if (maxSideMm > 0)
                {
                    foreach (var row in table.Rectangle)
                    {
                        if (maxSideMm >= row.MinMm && maxSideMm <= row.MaxMm)
                        {
                            return row.ThicknessMm.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
                        }
                    }
                }
            }

            return null;
        }

        private string? GetElementLevelName(Element elem)
        {
            ElementId levelId = elem.LevelId;
            if (levelId == ElementId.InvalidElementId)
            {
                Parameter? p = elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM) ??
                               elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM) ??
                               elem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (p != null && p.HasValue)
                {
                    levelId = p.AsElementId();
                }
            }

            if (levelId != ElementId.InvalidElementId)
            {
                Element? lvl = _doc.GetElement(levelId);
                return lvl?.Name;
            }

            return null;
        }

        private string? GetParameterStringValue(Element elem, ParamIdentifier paramId, ParamGettingMode mode, bool convertToMm = false, int? roundDec = null)
        {
            Element target = elem;

            if (mode == ParamGettingMode.Type)
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId == ElementId.InvalidElementId) return null;
                target = _doc.GetElement(typeId);
                if (target == null) return null;
            }

            Parameter? p = FindParameter(target, paramId);
            if (p == null || !p.HasValue) return null;

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString();

                case StorageType.Double:
                    double dVal = p.AsDouble();
                    if (convertToMm)
                    {
                        dVal *= 304.8;
                    }
                    if (roundDec.HasValue)
                    {
                        dVal = Math.Round(dVal, roundDec.Value);
                    }
                    return dVal.ToString(CultureInfo.InvariantCulture);

                case StorageType.Integer:
                    return p.AsInteger().ToString();

                case StorageType.ElementId:
                    ElementId id = p.AsElementId();
                    if (id != ElementId.InvalidElementId)
                    {
                        Element? refElem = _doc.GetElement(id);
                        return refElem?.Name ?? id.ToString();
                    }
                    return null;

                default:
                    return p.AsValueString();
            }
        }

        private bool SetTargetParameterValue(Element elem, ParamIdentifier targetParam, string value)
        {
            Parameter? p = FindParameter(elem, targetParam);
            if (p == null || p.IsReadOnly)
            {
                // Try setting on Type
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element? typeElem = _doc.GetElement(typeId);
                    if (typeElem != null)
                    {
                        p = FindParameter(typeElem, targetParam);
                    }
                }
            }

            if (p == null || p.IsReadOnly) return false;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value);
                        return true;

                    case StorageType.Double:
                        if (double.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                        {
                            p.Set(dVal);
                            return true;
                        }
                        break;

                    case StorageType.Integer:
                        if (int.TryParse(value, out int iVal))
                        {
                            p.Set(iVal);
                            return true;
                        }
                        break;
                }
            }
            catch { }

            return false;
        }

        private Parameter? FindParameter(Element elem, ParamIdentifier paramId)
        {
            if (paramId.Guid.HasValue)
            {
                Parameter? p = elem.get_Parameter(paramId.Guid.Value);
                if (p != null) return p;
            }

            if (paramId.BuiltInParamId.HasValue)
            {
                Parameter? p = elem.get_Parameter((BuiltInParameter)paramId.BuiltInParamId.Value);
                if (p != null) return p;
            }

            if (!string.IsNullOrEmpty(paramId.Name))
            {
                Parameter? p = elem.LookupParameter(paramId.Name);
                if (p != null) return p;
            }

            return null;
        }

        private string? GetBuiltInCategoryName(Category? cat)
        {
            if (cat == null) return null;
            try
            {
#if NET8_0_OR_GREATER
                return ((BuiltInCategory)cat.Id.Value).ToString();
#else
                return ((BuiltInCategory)cat.Id.IntegerValue).ToString();
#endif
            }
            catch
            {
                return null;
            }
        }
    }
}
