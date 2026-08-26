using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
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

            result.Messages.Add($"Успешно обработано: {result.UpdatedElementsCount} элементов обновлено.");
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
                string fragType = frag.FragmentType;

                if (fragType == "Const" || fragType == "String" || fragType == "Symbol")
                {
                    parts.Add(frag.StaticText ?? string.Empty);
                }
                else if (fragType == "ParameterValue" || fragType == "Param" || fragType == "Parameter")
                {
                    string? val = GetParameterStringValue(elem, frag.ParamId, frag.ParamMode, frag.ConvertToMillimeters, frag.RoundDecimal);
                    if (val != null) parts.Add(val);
                }
                else if (fragType == "StringOperation")
                {
                    string? rawVal = GetParameterStringValue(elem, frag.ParamId, frag.ParamMode, frag.ConvertToMillimeters, frag.RoundDecimal);
                    string processed = ApplyStringOperation(rawVal, frag.RawElement);
                    if (!string.IsNullOrEmpty(processed)) parts.Add(processed);
                }
                else if (fragType == "Calculator")
                {
                    string calcVal = EvaluateCalculator(elem, frag.RawElement);
                    if (!string.IsNullOrEmpty(calcVal)) parts.Add(calcVal);
                }
                else if (fragType == "DuctThickness")
                {
                    string? val = CalculateDuctThickness(elem, frag.DuctThicknessData ?? DuctThicknessTable.CreateDefault());
                    if (val != null) parts.Add(val);
                }
                else if (fragType == "PipeThickness")
                {
                    string? val = CalculatePipeThickness(elem);
                    if (val != null) parts.Add(val);
                }
                else if (fragType == "FloorLevelName" || fragType == "Level")
                {
                    string? levelName = GetElementLevelName(elem);
                    if (levelName != null) parts.Add(levelName);
                }
                else if (fragType == "MEPSize")
                {
                    string? sizeStr = GetMEPSizeString(elem);
                    if (sizeStr != null) parts.Add(sizeStr);
                }
                else if (fragType == "FittingAngle")
                {
                    string? angleStr = GetFittingAngleString(elem);
                    if (angleStr != null) parts.Add(angleStr);
                }
                else if (fragType == "MEPsystem01" || fragType == "MEPsystem02")
                {
                    string? sysStr = GetMEPSystemString(elem, frag.RawElement);
                    if (sysStr != null) parts.Add(sysStr);
                }
                else
                {
                    if (!string.IsNullOrEmpty(frag.StaticText)) parts.Add(frag.StaticText);
                }
            }

            return string.Concat(parts);
        }

        private string ApplyStringOperation(string? rawVal, XElement? rawEl)
        {
            if (string.IsNullOrEmpty(rawVal)) return string.Empty;
            if (rawEl == null) return rawVal;

            var opEl = rawEl.Element("strOperation");
            if (opEl == null) return rawVal;

            string opType = opEl.Attribute("type")?.Value ?? string.Empty;

            try
            {
                if (opType == "LeftN" && int.TryParse(opEl.Element("count")?.Value, out int leftCount))
                {
                    return rawVal.Length <= leftCount ? rawVal : rawVal.Substring(0, leftCount);
                }
                if (opType == "RightN" && int.TryParse(opEl.Element("count")?.Value, out int rightCount))
                {
                    return rawVal.Length <= rightCount ? rawVal : rawVal.Substring(rawVal.Length - rightCount);
                }
                if (opType == "IgnoreLeftN" && int.TryParse(opEl.Element("count")?.Value, out int ignLeft))
                {
                    return rawVal.Length > ignLeft ? rawVal.Substring(ignLeft) : string.Empty;
                }
                if (opType == "IgnoreRightN" && int.TryParse(opEl.Element("count")?.Value, out int ignRight))
                {
                    return rawVal.Length > ignRight ? rawVal.Substring(0, rawVal.Length - ignRight) : string.Empty;
                }
                if (opType == "Substring")
                {
                    int start = int.TryParse(opEl.Element("startIndex")?.Value, out int s) ? s : 0;
                    int length = int.TryParse(opEl.Element("length")?.Value, out int l) ? l : rawVal.Length;
                    if (start < rawVal.Length)
                    {
                        return rawVal.Substring(start, Math.Min(length, rawVal.Length - start));
                    }
                }
                if (opType == "Replace")
                {
                    string oldV = opEl.Element("oldValue")?.Value ?? string.Empty;
                    string newV = opEl.Element("newValue")?.Value ?? string.Empty;
                    if (!string.IsNullOrEmpty(oldV))
                    {
                        return rawVal.Replace(oldV, newV);
                    }
                }
                if (opType == "LetterCase")
                {
                    string letterCase = opEl.Element("case")?.Value ?? string.Empty;
                    if (letterCase == "Upper") return rawVal.ToUpperInvariant();
                    if (letterCase == "Lower") return rawVal.ToLowerInvariant();
                }
            }
            catch { }

            return rawVal;
        }

        private string EvaluateCalculator(Element elem, XElement? rawEl)
        {
            if (rawEl == null) return string.Empty;

            try
            {
                var firstEl = rawEl.Element("firstOperand");
                var secondEl = rawEl.Element("secondOperand");
                var opEl = rawEl.Element("operation");

                double v1 = GetOperandValue(elem, firstEl);
                double v2 = GetOperandValue(elem, secondEl);
                string op = opEl?.Value ?? "Add";

                double result = op switch
                {
                    "Multiply" => v1 * v2,
                    "Division" => v2 != 0 ? v1 / v2 : 0,
                    "Subtraction" => v1 - v2,
                    _ => v1 + v2
                };

                return result.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
            }
            catch
            {
                return string.Empty;
            }
        }

        private double GetOperandValue(Element elem, XElement? opEl)
        {
            if (opEl == null) return 0;

            string? constVal = opEl.Element("const")?.Value ?? opEl.Element("value")?.Value;
            if (!string.IsNullOrEmpty(constVal) && double.TryParse(constVal.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double cv))
            {
                return cv;
            }

            var paramEl = opEl.Element("paramId");
            if (paramEl != null)
            {
                string paramName = paramEl.Element("name")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(paramName))
                {
                    Parameter? p = elem.LookupParameter(paramName);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double)
                    {
                        return p.AsDouble();
                    }
                }
            }

            return 0;
        }

        private string? GetMEPSizeString(Element elem)
        {
            Parameter? p = elem.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE) ?? 
                           elem.get_Parameter(BuiltInParameter.RBS_REFERENCE_OVERALLSIZE) ??
                           elem.LookupParameter("Размер");
            if (p != null && p.HasValue)
            {
                return p.AsString() ?? p.AsValueString();
            }
            return null;
        }

        private string? GetFittingAngleString(Element elem)
        {
            Parameter? p = elem.LookupParameter("Угол") ?? 
                           elem.LookupParameter("Angle");
            if (p != null && p.HasValue && p.StorageType == StorageType.Double)
            {
                double angleDeg = p.AsDouble() * (180.0 / Math.PI);
                return angleDeg.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "°";
            }
            return null;
        }

        private string? GetMEPSystemString(Element elem, XElement? rawEl)
        {
            Parameter? p = elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM) ??
                           elem.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM) ??
                           elem.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM) ??
                           elem.LookupParameter("Имя системы") ??
                           elem.LookupParameter("Сокращение для системы");
            if (p != null && p.HasValue)
            {
                return p.AsString();
            }
            return null;
        }

        private string? CalculatePipeThickness(Element elem)
        {
            Parameter? diamParam = elem.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM) ?? elem.LookupParameter("Диаметр");
            if (diamParam != null && diamParam.HasValue)
            {
                double diamMm = diamParam.AsDouble() * 304.8;
                // Standard steel pipe thickness lookup table
                if (diamMm <= 20) return "2,0";
                if (diamMm <= 32) return "2,5";
                if (diamMm <= 50) return "3,0";
                if (diamMm <= 100) return "3,5";
                if (diamMm <= 150) return "4,0";
                return "4,5";
            }
            return null;
        }

        private string? CalculateDuctThickness(Element elem, DuctThicknessTable table)
        {
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
