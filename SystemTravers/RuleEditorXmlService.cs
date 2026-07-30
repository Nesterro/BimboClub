using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace BimboClub.RuleEditor
{
    public static class RuleEditorXmlService
    {
        public static RuleSetModel LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл правил не найден: {filePath}");
            }

            XDocument doc;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                doc = XDocument.Load(fs);
            }

            if (doc.Root != null && doc.Root.Name.LocalName.Equals("rules", StringComparison.OrdinalIgnoreCase))
            {
                // Загрузка формата LTools XML (<rules>)
                return LoadFromLToolsXml(doc, filePath);
            }
            else
            {
                // Загрузка формата BimboClub XML (<RuleSet>)
                XmlSerializer serializer = new XmlSerializer(typeof(RuleSetModel));
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return (RuleSetModel)serializer.Deserialize(fs);
                }
            }
        }

        private static RuleSetModel LoadFromLToolsXml(XDocument doc, string filePath)
        {
            var ruleSet = new RuleSetModel
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Description = doc.Root.Element("comment")?.Value ?? ""
            };

            var groupMap = new Dictionary<string, RuleGroupModel>(StringComparer.OrdinalIgnoreCase);

            int ruleIndex = 1;
            foreach (XElement xeRule in doc.Root.Elements("rule"))
            {
                bool isEnabled = true;
                XAttribute attrEnable = xeRule.Attribute("enable");
                if (attrEnable != null && bool.TryParse(attrEnable.Value, out bool parsedEnable))
                {
                    isEnabled = parsedEnable;
                }

                string ruleName = xeRule.Element("comment")?.Value;
                if (string.IsNullOrWhiteSpace(ruleName))
                {
                    ruleName = $"Правило #{ruleIndex}";
                }

                RuleItemModel item = new RuleItemModel
                {
                    Name = ruleName,
                    IsEnabled = isEnabled
                };

                // Parsing Filters & Target Categories
                List<string> categoryKeys = new List<string>();
                XElement xeFilters = xeRule.Element("filters");
                if (xeFilters != null)
                {
                    foreach (XElement xeFilter in xeFilters.Elements("filter"))
                    {
                        string fType = xeFilter.Attribute("filterType")?.Value ?? "";

                        if (fType.Equals("ByCategories", StringComparison.OrdinalIgnoreCase))
                        {
                            XElement xeCats = xeFilter.Element("categories");
                            if (xeCats != null)
                            {
                                foreach (XElement xeCat in xeCats.Elements("category"))
                                {
                                    if (!string.IsNullOrWhiteSpace(xeCat.Value))
                                    {
                                        categoryKeys.Add(xeCat.Value.Trim());
                                    }
                                }
                            }
                        }
                        else if (!fType.Equals("ElementIsElementType", StringComparison.OrdinalIgnoreCase))
                        {
                            // Условие фильтрации параметров
                            RuleConditionModel cond = ParseLToolsCondition(xeFilter, fType);
                            if (cond != null && !string.IsNullOrWhiteSpace(cond.ParameterName))
                            {
                                item.Conditions.Add(cond);
                            }
                        }
                    }
                }

                // Parsing Actions
                XElement xeAction = xeRule.Element("action");
                if (xeAction != null)
                {
                    string targetParam = xeAction.Element("paramId")?.Element("name")?.Value ?? "";
                    if (!string.IsNullOrWhiteSpace(targetParam))
                    {
                        RuleActionModel act = ParseLToolsAction(xeAction, targetParam);
                        if (act != null)
                        {
                            item.Actions.Add(act);
                        }
                    }
                }

                if (categoryKeys.Count == 0)
                {
                    categoryKeys.Add("OST_PipeCurves");
                }

                foreach (string catKey in categoryKeys)
                {
                    if (!groupMap.TryGetValue(catKey, out RuleGroupModel group))
                    {
                        string groupName = GetCategoryDisplayName(catKey);
                        group = new RuleGroupModel
                        {
                            Name = groupName,
                            CategoryKey = catKey,
                            IsEnabled = true
                        };
                        groupMap[catKey] = group;
                        ruleSet.Groups.Add(group);
                    }
                    group.Rules.Add(item);
                }

                ruleIndex++;
            }

            if (ruleSet.Groups.Count == 0)
            {
                ruleSet.Groups.Add(new RuleGroupModel { Name = "Общие правила", CategoryKey = "OST_PipeCurves" });
            }

            return ruleSet;
        }

        private static RuleConditionModel ParseLToolsCondition(XElement xeFilter, string filterType)
        {
            RuleConditionModel cond = new RuleConditionModel();

            // Param Getting Mode
            string modeStr = xeFilter.Element("paramGettingMode")?.Value ?? "";
            if (filterType.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0) cond.Mode = ParamGettingMode.Type;
            else if (filterType.IndexOf("Host", StringComparison.OrdinalIgnoreCase) >= 0) cond.Mode = ParamGettingMode.Host;
            else if (Enum.TryParse(modeStr, true, out ParamGettingMode parsedMode)) cond.Mode = parsedMode;
            else cond.Mode = ParamGettingMode.Self;

            // Parameter Name
            cond.ParameterName = xeFilter.Element("paramId")?.Element("name")?.Value ?? "";

            // Value
            cond.Value = xeFilter.Element("value")?.Value ?? xeFilter.Element("text")?.Value ?? "";

            // Operator
            string opStr = xeFilter.Attribute("operator")?.Value ?? xeFilter.Attribute("mode")?.Value ?? "";
            if (opStr.Equals("Equals", StringComparison.OrdinalIgnoreCase) || opStr.Equals("=", StringComparison.OrdinalIgnoreCase)) cond.Operator = ConditionOperator.Equals;
            else if (opStr.Equals("NotEquals", StringComparison.OrdinalIgnoreCase) || opStr.Equals("!=" , StringComparison.OrdinalIgnoreCase)) cond.Operator = ConditionOperator.NotEquals;
            else if (opStr.Contains("Contains")) cond.Operator = ConditionOperator.Contains;
            else if (opStr.Contains("StartsWith")) cond.Operator = ConditionOperator.StartsWith;
            else if (opStr.Contains("EndsWith")) cond.Operator = ConditionOperator.EndsWith;
            else if (opStr.Contains("GreaterThan")) cond.Operator = ConditionOperator.GreaterThan;
            else if (opStr.Contains("LessThan")) cond.Operator = ConditionOperator.LessThan;
            else cond.Operator = ConditionOperator.Equals;

            return cond;
        }

        private static RuleActionModel ParseLToolsAction(XElement xeAction, string targetParam)
        {
            RuleActionModel act = new RuleActionModel
            {
                TargetParameter = targetParam,
                ActionType = ActionType.CopyParameter,
                SourceMode = ParamGettingMode.Self
            };

            XElement xeFragments = xeAction.Element("fragments");
            if (xeFragments != null)
            {
                var frags = xeFragments.Elements("fragment").ToList();
                if (frags.Count == 1)
                {
                    XElement f = frags[0];
                    string type = f.Attribute("type")?.Value ?? "";

                    if (type.Equals("ParameterValue", StringComparison.OrdinalIgnoreCase))
                    {
                        act.ActionType = ActionType.CopyParameter;
                        act.SourceParameter = f.Element("paramId")?.Element("name")?.Value ?? "";
                        if (Enum.TryParse(f.Element("paramGettingMode")?.Value, true, out ParamGettingMode mode))
                        {
                            act.SourceMode = mode;
                        }
                    }
                    else if (type.Equals("Const", StringComparison.OrdinalIgnoreCase) || type.Equals("Symbol", StringComparison.OrdinalIgnoreCase))
                    {
                        act.ActionType = ActionType.ConstantValue;
                        act.ConstantValue = f.Element("text")?.Value ?? f.Value ?? "";
                    }
                    else if (type.Equals("DuctThickness", StringComparison.OrdinalIgnoreCase) || type.Equals("PipeThickness", StringComparison.OrdinalIgnoreCase))
                    {
                        act.ActionType = ActionType.CopyParameter;
                        act.SourceParameter = "Толщина стенки";
                    }
                    else if (type.Equals("MEPSize", StringComparison.OrdinalIgnoreCase))
                    {
                        act.ActionType = ActionType.CopyParameter;
                        act.SourceParameter = "Размер";
                    }
                    else
                    {
                        act.ActionType = ActionType.ConstantValue;
                        act.ConstantValue = f.Value ?? "";
                    }
                }
                else if (frags.Count > 1)
                {
                    // Составная строка (Префикс + Исходный параметр + Суффикс)
                    string prefix = "";
                    string suffix = "";
                    string srcParam = "";
                    ParamGettingMode srcMode = ParamGettingMode.Self;

                    bool foundParam = false;
                    foreach (var f in frags)
                    {
                        string type = f.Attribute("type")?.Value ?? "";
                        if (type.Equals("ParameterValue", StringComparison.OrdinalIgnoreCase))
                        {
                            srcParam = f.Element("paramId")?.Element("name")?.Value ?? "";
                            if (Enum.TryParse(f.Element("paramGettingMode")?.Value, true, out ParamGettingMode mode))
                            {
                                srcMode = mode;
                            }
                            foundParam = true;
                        }
                        else
                        {
                            string text = f.Element("text")?.Value ?? f.Value ?? "";
                            if (!foundParam) prefix += text;
                            else suffix += text;
                        }
                    }

                    if (foundParam)
                    {
                        act.ActionType = ActionType.CopyParameter;
                        act.SourceParameter = srcParam;
                        act.SourceMode = srcMode;
                        act.Prefix = prefix;
                        act.Suffix = suffix;
                    }
                    else
                    {
                        act.ActionType = ActionType.ConstantValue;
                        act.ConstantValue = prefix + suffix;
                    }
                }
            }

            return act;
        }

        private static string GetCategoryDisplayName(string catKey)
        {
            switch (catKey)
            {
                case "OST_PipeCurves": return "Трубопроводы";
                case "OST_PipeFitting": return "Соединительные детали трубопроводов";
                case "OST_PipeAccessory": return "Арматура трубопроводов";
                case "OST_DuctCurves": return "Воздуховоды";
                case "OST_DuctFitting": return "Соединительные детали воздуховодов";
                case "OST_DuctAccessory": return "Арматура воздуховодов";
                case "OST_DuctTerminal": return "Воздухораспределители";
                case "OST_MechanicalEquipment": return "Оборудование";
                case "OST_PlumbingFixtures": return "Сантехнические приборы";
                case "OST_CableTray": return "Кабельные лотки";
                case "OST_Conduit": return "Короба";
                case "OST_LightingFixtures": return "Осветительные приборы";
                case "OST_ElectricalEquipment": return "Электрооборудование";
                case "OST_Walls": return "Стены";
                case "OST_Floors": return "Перекрытия / Полы";
                case "OST_GenericModel": return "Обобщенные модели";
                default: return catKey;
            }
        }

        public static void SaveToFile(RuleSetModel ruleSet, string filePath)
        {
            if (ruleSet == null) throw new ArgumentNullException(nameof(ruleSet));

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(RuleSetModel));
            using (StreamWriter writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                serializer.Serialize(writer, ruleSet);
            }
        }

        public static RuleSetModel CreateDefaultRuleSet()
        {
            var ruleSet = new RuleSetModel
            {
                Name = "Базовый набор правил BimboClub",
                Description = "Правила заполнения наименований, кодов и спецификаций MEP систем"
            };

            var pipeGroup = new RuleGroupModel
            {
                Name = "Трубопроводы",
                CategoryKey = "OST_PipeCurves",
                IsEnabled = true
            };

            pipeGroup.Rules.Add(new RuleItemModel
            {
                Name = "Заполнение наименования стальных труб",
                IsEnabled = true,
                Conditions = new List<RuleConditionModel>
                {
                    new RuleConditionModel
                    {
                        Mode = ParamGettingMode.Type,
                        ParameterName = "Имя типа",
                        Operator = ConditionOperator.Contains,
                        Value = "Сталь"
                    }
                },
                Actions = new List<RuleActionModel>
                {
                    new RuleActionModel
                    {
                        TargetParameter = "ADSK_Наименование",
                        ActionType = ActionType.CopyParameter,
                        SourceMode = ParamGettingMode.Type,
                        SourceParameter = "Имя типа",
                        Prefix = "Труба стальная ",
                        Suffix = " ГОСТ 10704-91"
                    }
                }
            });

            var ductGroup = new RuleGroupModel
            {
                Name = "Воздуховоды",
                CategoryKey = "OST_DuctCurves",
                IsEnabled = true
            };

            ductGroup.Rules.Add(new RuleItemModel
            {
                Name = "Заполнение наименования оцинкованных воздуховодов",
                IsEnabled = true,
                Conditions = new List<RuleConditionModel>
                {
                    new RuleConditionModel
                    {
                        Mode = ParamGettingMode.Type,
                        ParameterName = "Имя типа",
                        Operator = ConditionOperator.Contains,
                        Value = "Оцинкованная"
                    }
                },
                Actions = new List<RuleActionModel>
                {
                    new RuleActionModel
                    {
                        TargetParameter = "ADSK_Наименование",
                        ActionType = ActionType.CopyParameter,
                        SourceMode = ParamGettingMode.Type,
                        SourceParameter = "Имя типа",
                        Prefix = "Воздуховод оцинкованный "
                    }
                }
            });

            ruleSet.Groups.Add(pipeGroup);
            ruleSet.Groups.Add(ductGroup);
            return ruleSet;
        }
    }
}
