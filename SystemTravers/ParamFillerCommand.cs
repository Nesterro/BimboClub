using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class ParamFillerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Открываем окно редактора правил
                ParamFillerWindow window = new ParamFillerWindow();
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true && window.IsRunRequested)
                {
                    RulesDoc rulesDoc = window.Rules;
                    if (rulesDoc == null || rulesDoc.Groups.Count == 0)
                    {
                        TaskDialog.Show("Информация", "Список правил пуст.");
                        return Result.Succeeded;
                    }

                    int totalProcessed = 0;
                    int totalUpdated = 0;

                    using (Transaction trans = new Transaction(doc, "Заполнение спецификаций по правилам"))
                    {
                        trans.Start();

                        // Обрабатываем группы по категориям
                        foreach (var group in rulesDoc.Groups)
                        {
                            var activeRules = group.Rules.Where(r => r.IsEnabled).ToList();
                            if (activeRules.Count == 0) continue;

                            // Находим определение категории
                            var catDef = CategoryRegistry.Categories.FirstOrDefault(c => c.CategoryKey == group.CategoryKey);
                            if (catDef == null) continue;

                            // Выбираем элементы этой категории из документа
                            ElementMulticategoryFilter multiFilter = new ElementMulticategoryFilter(catDef.BuiltInCategories);
                            FilteredElementCollector collector = new FilteredElementCollector(doc)
                                .WherePasses(multiFilter)
                                .WhereElementIsNotElementType();

                            List<Element> elementsToProcess = collector.ToList();
                            totalProcessed += elementsToProcess.Count;

                            foreach (Element elem in elementsToProcess)
                            {
                                // Ищем первое правило, которому соответствует элемент
                                FillRule matchedRule = null;
                                foreach (var rule in activeRules)
                                {
                                    if (IsRuleMatch(elem, rule, doc))
                                    {
                                        matchedRule = rule;
                                        break;
                                    }
                                }

                                if (matchedRule != null)
                                {
                                    if (ApplyRuleMappings(elem, matchedRule, doc))
                                    {
                                        totalUpdated++;
                                    }
                                }
                            }
                        }

                        trans.Commit();
                    }

                    // Вывод красивого отчета результатов расчета
                    string summaryMsg = $"Обработка завершена!\n\n" +
                                       $"Элементов в выбранных категориях: {totalProcessed}\n" +
                                       $"Успешно заполнено параметров: {totalUpdated}";
                    
                    TaskDialog.Show("Заполнение параметров", summaryMsg);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка при выполнении ParamFillerCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private static bool IsRuleMatch(Element elem, FillRule rule, Document doc)
        {
            if (rule.Conditions == null || rule.Conditions.Count == 0) return true;

            foreach (var cond in rule.Conditions)
            {
                if (string.IsNullOrEmpty(cond.ParameterName)) continue;

                string val = GetTokenValue(elem, cond.ParameterName, doc);
                string targetVal = cond.Value ?? "";
                bool isMatch = false;

                if (cond.Operator == "Equals")
                {
                    isMatch = val.Equals(targetVal, StringComparison.OrdinalIgnoreCase);
                }
                else if (cond.Operator == "Contains")
                {
                    isMatch = val.IndexOf(targetVal, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else if (cond.Operator == "StartsWith")
                {
                    isMatch = val.StartsWith(targetVal, StringComparison.OrdinalIgnoreCase);
                }
                else if (cond.Operator == "EndsWith")
                {
                    isMatch = val.EndsWith(targetVal, StringComparison.OrdinalIgnoreCase);
                }
                else if (cond.Operator == "NotEmpty")
                {
                    isMatch = !string.IsNullOrEmpty(val);
                }

                if (!isMatch) return false; // Must match ALL conditions
            }

            return true;
        }

        private static bool ApplyRuleMappings(Element elem, FillRule rule, Document doc)
        {
            bool anyUpdated = false;

            foreach (var map in rule.Mappings)
            {
                if (string.IsNullOrEmpty(map.TargetParameter)) continue;

                Parameter p = elem.LookupParameter(map.TargetParameter);
                if (p == null || p.IsReadOnly) continue;

                // Evaluate formula template
                string evaluatedValue = map.Formula;
                if (evaluatedValue == null) evaluatedValue = "";
                
                // Identify all tokens in formula (like {Семейство}, {Размер})
                int startIdx = -1;
                while ((startIdx = evaluatedValue.IndexOf('{')) >= 0)
                {
                    int endIdx = evaluatedValue.IndexOf('}', startIdx);
                    if (endIdx < 0) break;

                    string token = evaluatedValue.Substring(startIdx + 1, endIdx - startIdx - 1);
                    string tokenValue = GetTokenValue(elem, token, doc);
                    evaluatedValue = evaluatedValue.Remove(startIdx, endIdx - startIdx + 1).Insert(startIdx, tokenValue);
                }

                // If target parameter is Quantity/Количество, check if formula has numeric math
                bool isQtyParam = map.TargetParameter.Equals("ADSK_Количество", StringComparison.OrdinalIgnoreCase) 
                               || map.TargetParameter.Equals("Количество", StringComparison.OrdinalIgnoreCase);

                if (isQtyParam)
                {
                    if (double.TryParse(evaluatedValue.Replace(',', '.').Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
                    {
                        anyUpdated |= WriteValue(p, parsedVal);
                    }
                    else
                    {
                        double val = 1.0;
                        if (map.Formula.Contains("{Длина}"))
                        {
                            Parameter lenP = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                            if (lenP != null && lenP.HasValue)
                            {
                                val = lenP.AsDouble() * 0.3048; // length in meters
                            }
                        }
                        
                        double factor = 1.0;
                        if (map.Formula.Contains("*"))
                        {
                            string[] parts = map.Formula.Split('*');
                            if (parts.Length > 1 && double.TryParse(parts[1].Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedFactor))
                            {
                                factor = parsedFactor;
                            }
                        }

                        anyUpdated |= WriteValue(p, val * factor);
                    }
                }
                else
                {
                    anyUpdated |= WriteValue(p, evaluatedValue);
                }
            }

            return anyUpdated;
        }

        private static bool WriteValue(Parameter p, object val)
        {
            try
            {
                if (p.StorageType == StorageType.String)
                {
                    return p.Set(val?.ToString() ?? "");
                }
                if (p.StorageType == StorageType.Double)
                {
                    if (val is double d) return p.Set(d);
                    if (double.TryParse(val?.ToString()?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedD))
                    {
                        return p.Set(parsedD);
                    }
                }
                if (p.StorageType == StorageType.Integer)
                {
                    if (val is int i) return p.Set(i);
                    if (val is double d) return p.Set((int)Math.Round(d));
                    if (int.TryParse(val?.ToString(), out int parsedI))
                    {
                        return p.Set(parsedI);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Не удалось записать значение в параметр {p.Definition.Name}", ex);
            }
            return false;
        }

        private static string GetTokenValue(Element elem, string tokenName, Document doc)
        {
            // Resolve element type
            ElementType type = null;
            ElementId typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                type = doc.GetElement(typeId) as ElementType;
            }

            if (tokenName.Equals("Семейство", StringComparison.OrdinalIgnoreCase))
            {
                return type != null ? type.FamilyName : "";
            }
            if (tokenName.Equals("Тип", StringComparison.OrdinalIgnoreCase) || 
                tokenName.Equals("Имя типа", StringComparison.OrdinalIgnoreCase))
            {
                return type != null ? type.Name : "";
            }
            if (tokenName.Equals("Размер", StringComparison.OrdinalIgnoreCase))
            {
                Parameter sizeP = elem.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
                if (sizeP != null && sizeP.HasValue) return sizeP.AsString();
                
                sizeP = elem.LookupParameter("Размер");
                if (sizeP != null && sizeP.HasValue) return sizeP.AsString();

                string w = GetTokenValue(elem, "Ширина", doc);
                string h = GetTokenValue(elem, "Высота", doc);
                if (!string.IsNullOrEmpty(w) && !string.IsNullOrEmpty(h)) return $"{w}x{h}";
                
                string d = GetTokenValue(elem, "Диаметр", doc);
                if (!string.IsNullOrEmpty(d)) return d;
                
                return elem.Name;
            }
            if (tokenName.Equals("Длина", StringComparison.OrdinalIgnoreCase))
            {
                Parameter lenP = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lenP != null && lenP.HasValue)
                {
                    double meters = lenP.AsDouble() * 0.3048;
                    return Math.Round(meters, 2).ToString("0.##");
                }
                return "1";
            }
            if (tokenName.Equals("Ширина", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM) 
                           ?? elem.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM)
                           ?? elem.LookupParameter("Ширина")
                           ?? elem.LookupParameter("Width");
                if (p != null && p.HasValue) return p.AsValueString() ?? p.AsDouble().ToString();
            }
            if (tokenName.Equals("Высота", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM) 
                           ?? elem.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM)
                           ?? elem.LookupParameter("Высота")
                           ?? elem.LookupParameter("Height");
                if (p != null && p.HasValue) return p.AsValueString() ?? p.AsDouble().ToString();
            }
            if (tokenName.Equals("Диаметр", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER) 
                           ?? elem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)
                           ?? elem.LookupParameter("Диаметр")
                           ?? elem.LookupParameter("Diameter");
                if (p != null && p.HasValue) return p.AsValueString() ?? p.AsDouble().ToString();
            }
            if (tokenName.Equals("Внешний диаметр", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)
                           ?? elem.LookupParameter("Внешний диаметр");
                if (p != null && p.HasValue) return p.AsValueString() ?? p.AsDouble().ToString();
            }
            if (tokenName.Equals("Толщина стенки", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.LookupParameter("Толщина стенки")
                           ?? elem.LookupParameter("WallThickness");
                if (p != null && p.HasValue) return p.AsValueString() ?? p.AsDouble().ToString();
            }
            if (tokenName.Equals("Угол", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.LookupParameter("Угол")
                           ?? elem.LookupParameter("Angle")
                           ?? elem.LookupParameter("Угол поворота");
                if (p != null && p.HasValue)
                {
                    double rad = p.AsDouble();
                    double deg = rad * (180.0 / Math.PI);
                    return Math.Round(deg).ToString("0");
                }
            }
            if (tokenName.Equals("Имя системы", StringComparison.OrdinalIgnoreCase) || 
                tokenName.Equals("Система", StringComparison.OrdinalIgnoreCase))
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (p != null && p.HasValue) return p.AsString();
            }

            // Fallback: look up custom parameter on element or its type
            Parameter customP = elem.LookupParameter(tokenName);
            if (customP == null || !customP.HasValue)
            {
                if (type != null) customP = type.LookupParameter(tokenName);
            }
            if (customP != null && customP.HasValue)
            {
                if (customP.StorageType == StorageType.String) return customP.AsString();
                return customP.AsValueString() ?? customP.AsDouble().ToString();
            }

            return "";
        }
    }
}
