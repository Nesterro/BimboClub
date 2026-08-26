using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Rules
{
    public enum FilterMatchType
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        IsEmpty,
        IsNotEmpty
    }

    public enum RuleScope
    {
        EntireDocument,
        ActiveView,
        Selection
    }

    public enum ParamGettingMode
    {
        Self,
        Type,
        Host,
        HostType,
        Parent
    }

    public class ParamIdentifier : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private Guid? _guid;
        private int? _builtInParamId;
        private StorageType _storageType = StorageType.String;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public Guid? Guid
        {
            get => _guid;
            set { _guid = value; OnPropertyChanged(); }
        }

        public int? BuiltInParamId
        {
            get => _builtInParamId;
            set { _builtInParamId = value; OnPropertyChanged(); }
        }

        public StorageType StorageType
        {
            get => _storageType;
            set { _storageType = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => Name;
    }

    public class RuleFilter : INotifyPropertyChanged
    {
        private string _filterType = "ByCategories"; // ByCategories, ElementIsElementType, ByParameterValue, ByInsulationParameterValue, ByLevel
        private bool _inverted;
        private ObservableCollection<string> _categories = new();
        private ParamIdentifier _paramId = new();
        private FilterMatchType _matchType = FilterMatchType.Equals;
        private string _value = string.Empty;
        public XElement? RawElement { get; set; }

        public string FilterType
        {
            get => _filterType;
            set { _filterType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public bool Inverted
        {
            get => _inverted;
            set { _inverted = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public ObservableCollection<string> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public ParamIdentifier ParamId
        {
            get => _paramId;
            set { _paramId = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public FilterMatchType MatchType
        {
            get => _matchType;
            set { _matchType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName
        {
            get
            {
                if (FilterType == "ByCategories")
                {
                    string cats = Categories.Count > 0 ? string.Join(", ", Categories) : "Любые";
                    return $"Категории: {cats}";
                }
                if (FilterType == "ElementIsElementType")
                {
                    return Inverted ? "Только экземпляры (не типы)" : "Только типоразмеры";
                }
                if (FilterType == "ByParameterValue" || FilterType == "ByInsulationParameterValue")
                {
                    string prefix = FilterType == "ByInsulationParameterValue" ? "[Изоляция] " : "";
                    string op = MatchType switch
                    {
                        FilterMatchType.Equals => "=",
                        FilterMatchType.NotEquals => "≠",
                        FilterMatchType.Contains => "содержит",
                        FilterMatchType.NotContains => "не содержит",
                        FilterMatchType.StartsWith => "начинается с",
                        FilterMatchType.GreaterThan => ">",
                        FilterMatchType.LessThan => "<",
                        FilterMatchType.IsEmpty => "пусто",
                        FilterMatchType.IsNotEmpty => "не пусто",
                        _ => MatchType.ToString()
                    };
                    return $"{prefix}{ParamId.Name} {op} '{Value}'";
                }
                return FilterType;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class DuctSizeRow
    {
        public double MinMm { get; set; }
        public double MaxMm { get; set; } = double.MaxValue;
        public double ThicknessMm { get; set; }
    }

    public class DuctThicknessTable
    {
        public List<DuctSizeRow> Rectangle { get; set; } = new();
        public List<DuctSizeRow> Round { get; set; } = new();
        public bool ConvertToMillimeters { get; set; } = true;

        public static DuctThicknessTable CreateDefault()
        {
            var table = new DuctThicknessTable();
            // Прямоугольные
            table.Rectangle.Add(new DuctSizeRow { MinMm = 0, MaxMm = 250, ThicknessMm = 0.5 });
            table.Rectangle.Add(new DuctSizeRow { MinMm = 250, MaxMm = 1000, ThicknessMm = 0.7 });
            table.Rectangle.Add(new DuctSizeRow { MinMm = 1000, MaxMm = 2000, ThicknessMm = 0.9 });
            table.Rectangle.Add(new DuctSizeRow { MinMm = 2000, MaxMm = 99999, ThicknessMm = 0.9 });

            // Круглые
            table.Round.Add(new DuctSizeRow { MinMm = 0, MaxMm = 200, ThicknessMm = 0.5 });
            table.Round.Add(new DuctSizeRow { MinMm = 200, MaxMm = 450, ThicknessMm = 0.6 });
            table.Round.Add(new DuctSizeRow { MinMm = 450, MaxMm = 800, ThicknessMm = 0.7 });
            table.Round.Add(new DuctSizeRow { MinMm = 800, MaxMm = 1250, ThicknessMm = 1.0 });
            table.Round.Add(new DuctSizeRow { MinMm = 1250, MaxMm = 1600, ThicknessMm = 1.2 });
            table.Round.Add(new DuctSizeRow { MinMm = 1600, MaxMm = 2000, ThicknessMm = 1.4 });
            table.Round.Add(new DuctSizeRow { MinMm = 2000, MaxMm = 99999, ThicknessMm = 1.4 });
            return table;
        }
    }

    public class RuleFragment : INotifyPropertyChanged
    {
        private string _fragmentType = "Const"; // Const, ParameterValue, StringOperation, Calculator, DuctThickness, PipeThickness, FloorLevelName, MEPSize, FittingAngle, MEPsystem01
        private string _staticText = string.Empty;
        private string _description = string.Empty;
        private ParamIdentifier _paramId = new();
        private ParamGettingMode _paramMode = ParamGettingMode.Self;
        private bool _convertToMillimeters = true;
        private int? _roundDecimal;
        private DuctThicknessTable? _ductThicknessData;
        public XElement? RawElement { get; set; }

        public string FragmentType
        {
            get => _fragmentType;
            set { _fragmentType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string StaticText
        {
            get => _staticText;
            set { _staticText = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public ParamIdentifier ParamId
        {
            get => _paramId;
            set { _paramId = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public ParamGettingMode ParamMode
        {
            get => _paramMode;
            set { _paramMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public bool ConvertToMillimeters
        {
            get => _convertToMillimeters;
            set { _convertToMillimeters = value; OnPropertyChanged(); }
        }

        public int? RoundDecimal
        {
            get => _roundDecimal;
            set { _roundDecimal = value; OnPropertyChanged(); }
        }

        public DuctThicknessTable? DuctThicknessData
        {
            get => _ductThicknessData;
            set { _ductThicknessData = value; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Description)) return Description;

                if (FragmentType == "Const" || FragmentType == "String" || FragmentType == "Symbol")
                    return $"Текст: \"{StaticText}\"";
                if (FragmentType == "ParameterValue" || FragmentType == "Parameter" || FragmentType == "Param")
                    return $"Параметр [{ParamMode}]: {ParamId.Name}";
                if (FragmentType == "StringOperation")
                    return $"Операция над строкой: {ParamId.Name}";
                if (FragmentType == "Calculator")
                    return "Математический расчет";
                if (FragmentType == "DuctThickness")
                    return "Толщина стенок воздуховода (по размерам)";
                if (FragmentType == "PipeThickness")
                    return "Толщина стенок трубы (по диаметру)";
                if (FragmentType == "FloorLevelName" || FragmentType == "Level")
                    return "Имя этажа / уровня";
                if (FragmentType == "MEPSize")
                    return "Размер MEP элемента";
                if (FragmentType == "FittingAngle")
                    return "Угол фитинга";
                if (FragmentType == "MEPsystem01" || FragmentType == "MEPsystem02")
                    return "Параметр системы MEP";

                return FragmentType;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RuleItem : INotifyPropertyChanged
    {
        private int _index;
        private bool _isEnabled = true;
        private string _name = "Новое правило";
        private string _comment = string.Empty;
        private string _formulaDescription = string.Empty;
        private ParamIdentifier _targetParam = new();
        private ObservableCollection<RuleFragment> _fragments = new();
        private ObservableCollection<RuleFilter> _filters = new();

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Comment
        {
            get => _comment;
            set { _comment = value; OnPropertyChanged(); }
        }

        public string FormulaDescription
        {
            get => _formulaDescription;
            set { _formulaDescription = value; OnPropertyChanged(); }
        }

        public ParamIdentifier TargetParam
        {
            get => _targetParam;
            set { _targetParam = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RuleFragment> Fragments
        {
            get => _fragments;
            set { _fragments = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RuleFilter> Filters
        {
            get => _filters;
            set { _filters = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RuleSet : INotifyPropertyChanged
    {
        private string _comment = string.Empty;
        private string _filePath = string.Empty;
        private RuleScope _scope = RuleScope.EntireDocument;
        private ObservableCollection<RuleItem> _rules = new();

        public string Comment
        {
            get => _comment;
            set { _comment = value; OnPropertyChanged(); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(); }
        }

        public RuleScope Scope
        {
            get => _scope;
            set { _scope = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RuleItem> Rules
        {
            get => _rules;
            set { _rules = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
