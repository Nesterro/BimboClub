using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Autodesk.Revit.DB;

namespace BimboClub
{
    [DataContract]
    public class RulesDoc
    {
        [DataMember]
        public List<CategoryRuleGroup> Groups { get; set; } = new List<CategoryRuleGroup>();
    }

    [DataContract]
    public class CategoryRuleGroup
    {
        [DataMember]
        public string CategoryKey { get; set; } // e.g. "OST_DuctCurves"
        
        [DataMember]
        public List<FillRule> Rules { get; set; } = new List<FillRule>();
    }

    [DataContract]
    public class FillRule
    {
        [DataMember]
        public string RuleName { get; set; } = "Новое правило";
        
        [DataMember]
        public bool IsEnabled { get; set; } = true;

        [DataMember]
        public List<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();

        [DataMember]
        public List<ParamMapping> Mappings { get; set; } = new List<ParamMapping>();
    }

    [DataContract]
    public class RuleCondition
    {
        [DataMember]
        public string ParameterName { get; set; } = "Имя типа";

        [DataMember]
        public string Operator { get; set; } = "Contains"; // "Equals", "Contains", "StartsWith", "EndsWith", "NotEmpty"

        [DataMember]
        public string Value { get; set; } = "";

        public override string ToString()
        {
            return $"{ParameterName} {Operator} \"{Value}\"";
        }
    }

    [DataContract]
    public class ParamMapping
    {
        [DataMember]
        public string TargetParameter { get; set; } = "ADSK_Наименование";

        [DataMember]
        public string Formula { get; set; } = "";

        public override string ToString()
        {
            return $"{TargetParameter} = {Formula}";
        }
    }

    public class CategoryDef
    {
        public string DisplayName { get; set; }
        public string CategoryKey { get; set; }
        public List<BuiltInCategory> BuiltInCategories { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class CategoryRegistry
    {
        public static readonly List<CategoryDef> Categories = new List<CategoryDef>
        {
            new CategoryDef { DisplayName = "Воздуховоды", CategoryKey = "OST_DuctCurves", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_DuctCurves } },
            new CategoryDef { DisplayName = "Детали воздуховодов", CategoryKey = "OST_DuctFitting", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_DuctFitting } },
            new CategoryDef { DisplayName = "Трубопроводы", CategoryKey = "OST_PipeCurves", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_PipeCurves } },
            new CategoryDef { DisplayName = "Детали трубопроводов", CategoryKey = "OST_PipeFitting", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_PipeFitting } },
            new CategoryDef { DisplayName = "Кабельные лотки", CategoryKey = "OST_CableTray", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_CableTray } },
            new CategoryDef { DisplayName = "Детали лотков", CategoryKey = "OST_CableTrayFitting", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_CableTrayFitting } },
            new CategoryDef { DisplayName = "Арматура MEP", CategoryKey = "OST_MepAccessory", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_PipeAccessory } },
            new CategoryDef { DisplayName = "Оборудование", CategoryKey = "OST_MechanicalEquipment", BuiltInCategories = new List<BuiltInCategory>{ BuiltInCategory.OST_MechanicalEquipment } }
        };
    }
}
