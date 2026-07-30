using System;
using System.Collections.Generic;
using System.IO;
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

            XmlSerializer serializer = new XmlSerializer(typeof(RuleSetModel));
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return (RuleSetModel)serializer.Deserialize(fs);
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

            // Группа: Трубопроводы
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

            // Группа: Воздуховоды
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
