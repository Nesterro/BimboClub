using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub.RuleEditor
{
    public class RuleExecutionResult
    {
        public int TotalProcessed { get; set; }
        public int TotalUpdated { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    public class RuleEditorEngine
    {
        private readonly Document _doc;

        public RuleEditorEngine(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public RuleExecutionResult ExecuteRuleSet(RuleSetModel ruleSet, bool currentViewOnly, ElementSet selectedElements = null)
        {
            RuleExecutionResult result = new RuleExecutionResult();
            if (ruleSet == null || ruleSet.Groups == null || ruleSet.Groups.Count == 0)
            {
                result.Messages.Add("Набор правил пуст.");
                return result;
            }

            using (Transaction trans = new Transaction(_doc, "Применение правил BimboClub"))
            {
                trans.Start();

                foreach (var group in ruleSet.Groups)
                {
                    if (!group.IsEnabled) continue;
                    var activeRules = group.Rules.Where(r => r.IsEnabled).ToList();
                    if (activeRules.Count == 0) continue;

                    List<Element> elementsToProcess = GetGroupElements(group.CategoryKey, currentViewOnly, selectedElements);
                    result.TotalProcessed += elementsToProcess.Count;

                    foreach (Element elem in elementsToProcess)
                    {
                        foreach (var rule in activeRules)
                        {
                            if (IsRuleMatch(elem, rule))
                            {
                                if (ApplyRuleActions(elem, rule))
                                {
                                    result.TotalUpdated++;
                                }
                                break; // В рамках группы применяется первое подошедшее правило
                            }
                        }
                    }
                }

                trans.Commit();
            }

            return result;
        }

        private List<Element> GetGroupElements(string categoryKey, bool currentViewOnly, ElementSet selectedElements)
        {
            List<Element> elements = new List<Element>();

            BuiltInCategory bic;
            if (!Enum.TryParse(categoryKey, out bic))
            {
                bic = BuiltInCategory.OST_PipeCurves;
            }

            FilteredElementCollector collector;
            if (selectedElements != null && selectedElements.Size > 0)
            {
                List<ElementId> ids = selectedElements.Cast<Element>().Select(e => e.Id).ToList();
                collector = new FilteredElementCollector(_doc, ids);
            }
            else if (currentViewOnly && _doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id);
            }
            else
            {
                collector = new FilteredElementCollector(_doc);
            }

            elements = collector
                .OfCategory(bic)
                .WhereElementIsNotElementType()
                .ToList();

            return elements;
        }

        private bool IsRuleMatch(Element elem, RuleItemModel rule)
        {
            if (rule.Conditions == null || rule.Conditions.Count == 0) return true;

            foreach (var cond in rule.Conditions)
            {
                if (!EvaluateCondition(elem, cond))
                {
                    return false;
                }
            }

            return true;
        }

        private bool EvaluateCondition(Element elem, RuleConditionModel cond)
        {
            string actualVal = GetParameterStringValue(elem, cond.Mode, cond.ParameterName);
            string targetVal = cond.Value ?? "";

            switch (cond.Operator)
            {
                case ConditionOperator.Equals:
                    return string.Equals(actualVal, targetVal, StringComparison.OrdinalIgnoreCase);

                case ConditionOperator.NotEquals:
                    return !string.Equals(actualVal, targetVal, StringComparison.OrdinalIgnoreCase);

                case ConditionOperator.Contains:
                    return actualVal.IndexOf(targetVal, StringComparison.OrdinalIgnoreCase) >= 0;

                case ConditionOperator.NotContains:
                    return actualVal.IndexOf(targetVal, StringComparison.OrdinalIgnoreCase) < 0;

                case ConditionOperator.StartsWith:
                    return actualVal.StartsWith(targetVal, StringComparison.OrdinalIgnoreCase);

                case ConditionOperator.EndsWith:
                    return actualVal.EndsWith(targetVal, StringComparison.OrdinalIgnoreCase);

                case ConditionOperator.IsEmpty:
                    return string.IsNullOrWhiteSpace(actualVal);

                case ConditionOperator.IsNotEmpty:
                    return !string.IsNullOrWhiteSpace(actualVal);

                case ConditionOperator.GreaterThan:
                    if (double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dActual1) &&
                        double.TryParse(targetVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dTarget1))
                    {
                        return dActual1 > dTarget1;
                    }
                    return string.Compare(actualVal, targetVal, StringComparison.OrdinalIgnoreCase) > 0;

                case ConditionOperator.LessThan:
                    if (double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dActual2) &&
                        double.TryParse(targetVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dTarget2))
                    {
                        return dActual2 < dTarget2;
                    }
                    return string.Compare(actualVal, targetVal, StringComparison.OrdinalIgnoreCase) < 0;

                default:
                    return true;
            }
        }

        private bool ApplyRuleActions(Element elem, RuleItemModel rule)
        {
            if (rule.Actions == null || rule.Actions.Count == 0) return false;

            bool anyUpdated = false;
            foreach (var action in rule.Actions)
            {
                if (string.IsNullOrWhiteSpace(action.TargetParameter)) continue;

                string newValue = CalculateActionValue(elem, action);
                if (SetParameterValue(elem, action.TargetParameter, newValue))
                {
                    anyUpdated = true;
                }
            }

            return anyUpdated;
        }

        private string CalculateActionValue(Element elem, RuleActionModel action)
        {
            switch (action.ActionType)
            {
                case ActionType.ConstantValue:
                    return action.ConstantValue ?? "";

                case ActionType.CopyParameter:
                case ActionType.StringConcat:
                    string val = GetParameterStringValue(elem, action.SourceMode, action.SourceParameter);
                    return $"{action.Prefix ?? ""}{val}{action.Suffix ?? ""}";

                case ActionType.StringReplace:
                    string orig = GetParameterStringValue(elem, action.SourceMode, action.SourceParameter);
                    if (!string.IsNullOrEmpty(action.SearchText))
                    {
                        return orig.Replace(action.SearchText, action.ReplaceText ?? "");
                    }
                    return orig;

                case ActionType.RoundDecimal:
                    string srcNum = GetParameterStringValue(elem, action.SourceMode, action.SourceParameter);
                    if (double.TryParse(srcNum, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal))
                    {
                        return Math.Round(parsedVal, action.RoundDigits).ToString(CultureInfo.InvariantCulture);
                    }
                    return srcNum;

                case ActionType.MathFormula:
                    return EvaluateFormula(elem, action.Formula);

                default:
                    return GetParameterStringValue(elem, action.SourceMode, action.SourceParameter);
            }
        }

        private string EvaluateFormula(Element elem, string formula)
        {
            if (string.IsNullOrWhiteSpace(formula)) return "";

            // Замена именованных параметров в формуле на их числовые значения
            string expr = formula;
            var matches = Regex.Matches(formula, @"\[([^\]]+)\]");
            foreach (Match match in matches)
            {
                string paramName = match.Groups[1].Value;
                string valStr = GetParameterStringValue(elem, ParamGettingMode.Self, paramName);
                if (!double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                {
                    dVal = 0;
                }
                expr = expr.Replace(match.Value, dVal.ToString(CultureInfo.InvariantCulture));
            }

            try
            {
                var dt = new System.Data.DataTable();
                var computeRes = dt.Compute(expr, "");
                return Convert.ToDouble(computeRes).ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                return expr;
            }
        }

        private string GetParameterStringValue(Element elem, ParamGettingMode mode, string paramName)
        {
            if (elem == null || string.IsNullOrWhiteSpace(paramName)) return "";

            Element targetElem = elem;
            if (mode == ParamGettingMode.Type)
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    targetElem = _doc.GetElement(typeId);
                }
            }
            else if (mode == ParamGettingMode.Host || mode == ParamGettingMode.HostType)
            {
                if (elem is FamilyInstance fi && fi.Host != null)
                {
                    targetElem = mode == ParamGettingMode.HostType ? _doc.GetElement(fi.Host.GetTypeId()) : fi.Host;
                }
            }

            if (targetElem == null) return "";

            if (paramName.Equals("Имя типа", StringComparison.OrdinalIgnoreCase) ||
                paramName.Equals("Type Name", StringComparison.OrdinalIgnoreCase))
            {
                return targetElem.Name ?? "";
            }

            Parameter p = targetElem.LookupParameter(paramName);
            if (p == null)
            {
                // Поиск по встроенным параметрам
                foreach (Parameter param in targetElem.Parameters)
                {
                    if (param.Definition != null && param.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                    {
                        p = param;
                        break;
                    }
                }
            }

            if (p == null) return "";

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString() ?? "";
                case StorageType.Integer:
                    return p.AsInteger().ToString();
                case StorageType.Double:
                    return p.AsValueString() ?? p.AsDouble().ToString(CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    Element idElem = _doc.GetElement(p.AsElementId());
                    return idElem?.Name ?? p.AsElementId().ToString();
                default:
                    return p.AsValueString() ?? "";
            }
        }

        private bool SetParameterValue(Element elem, string paramName, string value)
        {
            if (elem == null || string.IsNullOrWhiteSpace(paramName)) return false;

            Parameter p = elem.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return false;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value);
                        return true;

                    case StorageType.Integer:
                        if (int.TryParse(value, out int intVal))
                        {
                            p.Set(intVal);
                            return true;
                        }
                        break;

                    case StorageType.Double:
                        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                        {
                            p.Set(dblVal);
                            return true;
                        }
                        else
                        {
                            return p.SetValueString(value);
                        }

                    case StorageType.ElementId:
                        if (long.TryParse(value, out long idVal))
                        {
#if NET8_0_OR_GREATER
                            p.Set(new ElementId(idVal));
#else
                            p.Set(new ElementId((int)idVal));
#endif
                            return true;
                        }
                        break;
                }
            }
            catch { }

            return false;
        }
    }
}
