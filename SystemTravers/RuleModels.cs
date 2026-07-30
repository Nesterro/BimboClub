using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace BimboClub.RuleEditor
{
    public enum ParamGettingMode
    {
        Self,
        Type,
        Host,
        HostType,
        Parent
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        IsEmpty,
        IsNotEmpty
    }

    public enum ActionType
    {
        ConstantValue,
        CopyParameter,
        StringConcat,
        StringReplace,
        MathFormula,
        RoundDecimal
    }

    [Serializable]
    [XmlRoot("RuleSet")]
    public class RuleSetModel
    {
        [XmlAttribute("Name")]
        public string Name { get; set; } = "Набор правил BimboClub";

        [XmlAttribute("Description")]
        public string Description { get; set; } = "";

        [XmlElement("Group")]
        public List<RuleGroupModel> Groups { get; set; } = new List<RuleGroupModel>();
    }

    [Serializable]
    public class RuleGroupModel
    {
        [XmlAttribute("Name")]
        public string Name { get; set; } = "Новая группа";

        [XmlAttribute("CategoryKey")]
        public string CategoryKey { get; set; } = "OST_PipeCurves";

        [XmlAttribute("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        [XmlElement("Rule")]
        public List<RuleItemModel> Rules { get; set; } = new List<RuleItemModel>();
    }

    [Serializable]
    public class RuleItemModel
    {
        [XmlAttribute("Id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("Name")]
        public string Name { get; set; } = "Новое правило";

        [XmlAttribute("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        [XmlArray("Conditions")]
        [XmlArrayItem("Condition")]
        public List<RuleConditionModel> Conditions { get; set; } = new List<RuleConditionModel>();

        [XmlArray("Actions")]
        [XmlArrayItem("Action")]
        public List<RuleActionModel> Actions { get; set; } = new List<RuleActionModel>();
    }

    [Serializable]
    public class RuleConditionModel
    {
        [XmlAttribute("Mode")]
        public ParamGettingMode Mode { get; set; } = ParamGettingMode.Self;

        [XmlAttribute("ParameterName")]
        public string ParameterName { get; set; } = "";

        [XmlAttribute("Operator")]
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;

        [XmlAttribute("Value")]
        public string Value { get; set; } = "";

        public override string ToString()
        {
            string modeStr = Mode == ParamGettingMode.Self ? "" : $"[{Mode}] ";
            return $"{modeStr}{ParameterName} {GetOperatorSymbol(Operator)} \"{Value}\"";
        }

        public static string GetOperatorSymbol(ConditionOperator op)
        {
            switch (op)
            {
                case ConditionOperator.Equals: return "=";
                case ConditionOperator.NotEquals: return "≠";
                case ConditionOperator.Contains: return "содержит";
                case ConditionOperator.NotContains: return "не содержит";
                case ConditionOperator.StartsWith: return "начинается с";
                case ConditionOperator.EndsWith: return "заканчивается на";
                case ConditionOperator.GreaterThan: return ">";
                case ConditionOperator.LessThan: return "<";
                case ConditionOperator.IsEmpty: return "пусто";
                case ConditionOperator.IsNotEmpty: return "не пусто";
                default: return "=";
            }
        }
    }

    [Serializable]
    public class RuleActionModel
    {
        [XmlAttribute("TargetParameter")]
        public string TargetParameter { get; set; } = "";

        [XmlAttribute("ActionType")]
        public ActionType ActionType { get; set; } = ActionType.CopyParameter;

        [XmlAttribute("SourceMode")]
        public ParamGettingMode SourceMode { get; set; } = ParamGettingMode.Self;

        [XmlAttribute("SourceParameter")]
        public string SourceParameter { get; set; } = "";

        [XmlAttribute("ConstantValue")]
        public string ConstantValue { get; set; } = "";

        [XmlAttribute("Prefix")]
        public string Prefix { get; set; } = "";

        [XmlAttribute("Suffix")]
        public string Suffix { get; set; } = "";

        [XmlAttribute("SearchText")]
        public string SearchText { get; set; } = "";

        [XmlAttribute("ReplaceText")]
        public string ReplaceText { get; set; } = "";

        [XmlAttribute("Formula")]
        public string Formula { get; set; } = "";

        [XmlAttribute("RoundDigits")]
        public int RoundDigits { get; set; } = 2;

        public override string ToString()
        {
            switch (ActionType)
            {
                case ActionType.ConstantValue:
                    return $"{TargetParameter} = \"{ConstantValue}\"";
                case ActionType.CopyParameter:
                    string modeStr = SourceMode == ParamGettingMode.Self ? "" : $"[{SourceMode}]";
                    return $"{TargetParameter} = {Prefix}{modeStr}{SourceParameter}{Suffix}";
                case ActionType.StringReplace:
                    return $"{TargetParameter} = Замена в {SourceParameter}: \"{SearchText}\" -> \"{ReplaceText}\"";
                case ActionType.MathFormula:
                    return $"{TargetParameter} = Формула: {Formula}";
                case ActionType.RoundDecimal:
                    return $"{TargetParameter} = Округление({SourceParameter}, {RoundDigits} знаков)";
                default:
                    return $"{TargetParameter} = {SourceParameter}";
            }
        }
    }
}
