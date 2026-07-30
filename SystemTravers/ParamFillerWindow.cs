using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class ParamFillerWindow : Window
    {
        public bool IsRunRequested { get; private set; } = false;
        public RulesDoc Rules => _rulesDoc;

        private RulesDoc _rulesDoc;
        private CategoryDef _selectedCategory;
        private FillRule _selectedRule;

        // UI Controls
        private ListBox _lbCategories;
        private ListBox _lbRules;

        private Button _btnDeleteRule;
        private Button _btnDuplicateRule;
        private TextBox _txtRuleName;
        private CheckBox _chkRuleEnabled;

        // Tab Control
        private TabControl _tabControl;

        // Conditions Tab Controls
        private ListBox _lbConditions;
        private TextBox _txtCondParam;
        private ComboBox _cbCondOperator;
        private TextBox _txtCondValue;
        private Button _btnDeleteCond;

        // Mappings Tab Controls
        private ListBox _lbMappings;
        private TextBox _txtMapParam;
        private TextBox _txtMapFormula;
        private Button _btnDeleteMap;

        public ParamFillerWindow()
        {
            Title = "Редактор правил спецификаций - BimboClub Tools";
            Width = 840;
            Height = 580;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            InitializeRulesDoc();
            LoadDefaultRules();

            // Gradients & Brushes
            LinearGradientBrush headerGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 0.0);
            LinearGradientBrush redBtnGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush redBtnHover = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);

            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52));
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(30, 30, 35));
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(38, 38, 44));
            Style roundedButtonStyle = GetRoundedButtonStyle(4);
            Style listBoxStyle = GetListBoxStyle(controlBg, borderBrush);
            Style textBoxStyle = GetTextBoxStyle(controlBg, borderBrush);

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });    // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Footer
            Content = mainGrid;

            // --- 1. HEADER ---
            Border headerBorder = new Border { Background = headerGrad, Padding = new Thickness(20, 0, 20, 0) };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerTitle = new TextBlock
            {
                Text = "РЕДАКТОР ПРАВИЛ И СПЕЦИФИКАЦИЙ СИСТЕМ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerTitle;

            // --- 2. CONTENT LAYOUT ---
            Grid contentGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Left: Categories
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });  // Split
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Middle: Rules
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });  // Split
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right: Editor
            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // 2.1 COLUMN 1: CATEGORIES
            Grid catGrid = new Grid();
            catGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            catGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(catGrid, 0);
            contentGrid.Children.Add(catGrid);

            catGrid.Children.Add(CreateLabel("1. Категория систем:"));
            _lbCategories = new ListBox { Style = listBoxStyle };
            foreach (var cat in CategoryRegistry.Categories) _lbCategories.Items.Add(cat);
            _lbCategories.SelectionChanged += LbCategories_SelectionChanged;
            Grid.SetRow(_lbCategories, 1);
            catGrid.Children.Add(_lbCategories);

            // 2.2 COLUMN 2: RULES LIST
            Grid rulesGrid = new Grid();
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) }); // Buttons row
            Grid.SetColumn(rulesGrid, 2);
            contentGrid.Children.Add(rulesGrid);

            rulesGrid.Children.Add(CreateLabel("2. Правила категории:"));
            
            _lbRules = new ListBox { Style = listBoxStyle };
            _lbRules.SelectionChanged += LbRules_SelectionChanged;
            Grid.SetRow(_lbRules, 1);
            rulesGrid.Children.Add(_lbRules);

            // Buttons row for rules
            Grid ruleButtonsGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            ruleButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ruleButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            ruleButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ruleButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            ruleButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(ruleButtonsGrid, 2);
            rulesGrid.Children.Add(ruleButtonsGrid);

            Button btnAddRule = new Button { Content = "Добавить", Foreground = Brushes.White, Height = 26, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            ApplyFlatButton(btnAddRule, new SolidColorBrush(Color.FromRgb(50, 150, 80)), new SolidColorBrush(Color.FromRgb(70, 180, 100)), roundedButtonStyle);
            btnAddRule.Click += BtnAddRule_Click;
            Grid.SetColumn(btnAddRule, 0);
            ruleButtonsGrid.Children.Add(btnAddRule);

            _btnDeleteRule = new Button { Content = "Удалить", Foreground = Brushes.White, Height = 26, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = false };
            ApplyFlatButton(_btnDeleteRule, new SolidColorBrush(Color.FromRgb(150, 50, 50)), new SolidColorBrush(Color.FromRgb(180, 70, 70)), roundedButtonStyle);
            _btnDeleteRule.Click += BtnDeleteRule_Click;
            Grid.SetColumn(_btnDeleteRule, 2);
            ruleButtonsGrid.Children.Add(_btnDeleteRule);

            _btnDuplicateRule = new Button { Content = "Дублировать", Foreground = Brushes.White, Height = 26, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = false, FontSize = 10.5 };
            ApplyFlatButton(_btnDuplicateRule, new SolidColorBrush(Color.FromRgb(60, 60, 68)), new SolidColorBrush(Color.FromRgb(80, 80, 88)), roundedButtonStyle);
            _btnDuplicateRule.Click += BtnDuplicateRule_Click;
            Grid.SetColumn(_btnDuplicateRule, 4);
            ruleButtonsGrid.Children.Add(_btnDuplicateRule);

            // 2.3 COLUMN 3: SELECTED RULE EDITOR
            Border editorBorder = new Border { Background = panelBg, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(12) };
            Grid.SetColumn(editorBorder, 4);
            contentGrid.Children.Add(editorBorder);

            Grid editorGrid = new Grid();
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Title / Enable checkbox
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Rule Name TextBox
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // Split
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // TabControl (Conditions / Mappings)
            editorBorder.Child = editorGrid;

            // Header of editor
            Grid editorHeaderGrid = new Grid();
            editorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            Grid.SetRow(editorHeaderGrid, 0);
            editorGrid.Children.Add(editorHeaderGrid);

            TextBlock lblEditorTitle = CreateLabel("Настройка выбранного правила:");
            Grid.SetColumn(lblEditorTitle, 0);
            editorHeaderGrid.Children.Add(lblEditorTitle);

            _chkRuleEnabled = new CheckBox { Content = "Активно", Foreground = Brushes.White, IsChecked = true, VerticalAlignment = VerticalAlignment.Center, IsEnabled = false };
            _chkRuleEnabled.Checked += (s, e) => { if (_selectedRule != null) _selectedRule.IsEnabled = true; };
            _chkRuleEnabled.Unchecked += (s, e) => { if (_selectedRule != null) _selectedRule.IsEnabled = false; };
            Grid.SetColumn(_chkRuleEnabled, 1);
            editorHeaderGrid.Children.Add(_chkRuleEnabled);

            // Rule Name field
            _txtRuleName = new TextBox { Style = textBoxStyle, Height = 24, IsEnabled = false };
            _txtRuleName.TextChanged += TxtRuleName_TextChanged;
            Grid.SetRow(_txtRuleName, 1);
            editorGrid.Children.Add(_txtRuleName);

            // Tabs for Conditions and Mappings
            _tabControl = new TabControl { Background = panelBg, BorderBrush = borderBrush, Margin = new Thickness(0, 5, 0, 0), IsEnabled = false };
            Grid.SetRow(_tabControl, 3);
            editorGrid.Children.Add(_tabControl);

            // Tab 1: Conditions
            TabItem tabCond = new TabItem { Header = "Условия фильтра", Foreground = Brushes.White };
            _tabControl.Items.Add(tabCond);

            Grid condGrid = new Grid { Margin = new Thickness(5) };
            condGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Conditions List
            condGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(95) });                  // Editor section
            tabCond.Content = condGrid;

            _lbConditions = new ListBox { Style = listBoxStyle, Height = 130 };
            _lbConditions.SelectionChanged += LbConditions_SelectionChanged;
            condGrid.Children.Add(_lbConditions);

            // Editor form for selected condition
            Grid condEditorGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            condEditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            condEditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            condEditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            condEditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            condEditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            condEditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(condEditorGrid, 1);
            condGrid.Children.Add(condEditorGrid);

            _txtCondParam = new TextBox { Style = textBoxStyle, Height = 22 };
            _txtCondParam.TextChanged += TxtCondParam_TextChanged;
            Grid.SetColumn(_txtCondParam, 0); Grid.SetRow(_txtCondParam, 0);
            condEditorGrid.Children.Add(_txtCondParam);

            _cbCondOperator = new ComboBox { Style = GetComboBoxStyle(), Height = 22 };
            UiThemeHelper.StyleComboBoxDark(_cbCondOperator, controlBg, borderBrush);
            _cbCondOperator.Items.Add("Contains");
            _cbCondOperator.Items.Add("Equals");
            _cbCondOperator.Items.Add("StartsWith");
            _cbCondOperator.Items.Add("EndsWith");
            _cbCondOperator.Items.Add("NotEmpty");
            _cbCondOperator.SelectedIndex = 0;
            _cbCondOperator.SelectionChanged += CbCondOperator_SelectionChanged;
            Grid.SetColumn(_cbCondOperator, 2); Grid.SetRow(_cbCondOperator, 0);
            condEditorGrid.Children.Add(_cbCondOperator);

            TextBlock lblCondVal = new TextBlock { Text = "Значение:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(lblCondVal, 0); Grid.SetRow(lblCondVal, 1);
            condEditorGrid.Children.Add(lblCondVal);

            _txtCondValue = new TextBox { Style = textBoxStyle, Height = 22 };
            _txtCondValue.TextChanged += TxtCondValue_TextChanged;
            Grid.SetColumn(_txtCondValue, 2); Grid.SetRow(_txtCondValue, 1);
            condEditorGrid.Children.Add(_txtCondValue);

            // Cond buttons
            StackPanel condActionStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetColumnSpan(condActionStack, 3); Grid.SetRow(condActionStack, 2);
            condEditorGrid.Children.Add(condActionStack);

            Button btnAddCond = new Button { Content = "Добавить условие", Foreground = Brushes.White, Height = 22, Width = 130, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 5, 0), FontSize = 11 };
            ApplyFlatButton(btnAddCond, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnAddCond.Click += BtnAddCond_Click;
            condActionStack.Children.Add(btnAddCond);

            _btnDeleteCond = new Button { Content = "Удалить условие", Foreground = Brushes.White, Height = 22, Width = 120, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = false, FontSize = 11 };
            ApplyFlatButton(_btnDeleteCond, new SolidColorBrush(Color.FromRgb(150, 50, 50)), new SolidColorBrush(Color.FromRgb(180, 70, 70)), roundedButtonStyle);
            _btnDeleteCond.Click += BtnDeleteCond_Click;
            condActionStack.Children.Add(_btnDeleteCond);

            // Tab 2: Mappings (Parameters fill)
            TabItem tabMap = new TabItem { Header = "Запись параметров", Foreground = Brushes.White };
            _tabControl.Items.Add(tabMap);

            Grid mapGrid = new Grid { Margin = new Thickness(5) };
            mapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Mappings List
            mapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(95) });                 // Editor section
            tabMap.Content = mapGrid;

            _lbMappings = new ListBox { Style = listBoxStyle, Height = 130 };
            _lbMappings.SelectionChanged += LbMappings_SelectionChanged;
            mapGrid.Children.Add(_lbMappings);

            // Editor form for selected mapping
            Grid mapEditorGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            mapEditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            mapEditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            mapEditorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            mapEditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            mapEditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            mapEditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(mapEditorGrid, 1);
            mapGrid.Children.Add(mapEditorGrid);

            TextBlock lblMapParam = new TextBlock { Text = "Заполнить параметр:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(lblMapParam, 0); Grid.SetRow(lblMapParam, 0);
            mapEditorGrid.Children.Add(lblMapParam);

            _txtMapParam = new TextBox { Style = textBoxStyle, Height = 22 };
            _txtMapParam.TextChanged += TxtMapParam_TextChanged;
            Grid.SetColumn(_txtMapParam, 2); Grid.SetRow(_txtMapParam, 0);
            mapEditorGrid.Children.Add(_txtMapParam);

            TextBlock lblMapFormula = new TextBlock { Text = "Шаблон / Формула:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(lblMapFormula, 0); Grid.SetRow(lblMapFormula, 1);
            mapEditorGrid.Children.Add(lblMapFormula);

            _txtMapFormula = new TextBox { Style = textBoxStyle, Height = 22 };
            _txtMapFormula.TextChanged += TxtMapFormula_TextChanged;
            Grid.SetColumn(_txtMapFormula, 2); Grid.SetRow(_txtMapFormula, 1);
            mapEditorGrid.Children.Add(_txtMapFormula);

            // Map buttons
            StackPanel mapActionStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetColumnSpan(mapActionStack, 3); Grid.SetRow(mapActionStack, 2);
            mapEditorGrid.Children.Add(mapActionStack);

            Button btnAddMap = new Button { Content = "Добавить параметр", Foreground = Brushes.White, Height = 22, Width = 130, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 5, 0), FontSize = 11 };
            ApplyFlatButton(btnAddMap, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnAddMap.Click += BtnAddMap_Click;
            mapActionStack.Children.Add(btnAddMap);

            _btnDeleteMap = new Button { Content = "Удалить параметр", Foreground = Brushes.White, Height = 22, Width = 120, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsEnabled = false, FontSize = 11 };
            ApplyFlatButton(_btnDeleteMap, new SolidColorBrush(Color.FromRgb(150, 50, 50)), new SolidColorBrush(Color.FromRgb(180, 70, 70)), roundedButtonStyle);
            _btnDeleteMap.Click += BtnDeleteMap_Click;
            mapActionStack.Children.Add(_btnDeleteMap);

            // --- 3. FOOTER ACTIONS ---
            Grid footerGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            Grid.SetRow(footerGrid, 2);
            mainGrid.Children.Add(footerGrid);

            Button btnImport = new Button { Content = "Импорт правил", Foreground = Brushes.White, Height = 34, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            ApplyFlatButton(btnImport, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnImport.Click += BtnImport_Click;
            Grid.SetColumn(btnImport, 0);
            footerGrid.Children.Add(btnImport);

            Button btnExport = new Button { Content = "Экспорт правил", Foreground = Brushes.White, Height = 34, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0) };
            ApplyFlatButton(btnExport, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnExport.Click += BtnExport_Click;
            Grid.SetColumn(btnExport, 1);
            footerGrid.Children.Add(btnExport);

            Button btnRun = new Button
            {
                Content = "Запустить расчет",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 34,
                Cursor = Cursors.Hand
            };
            btnRun.Style = roundedButtonStyle;
            btnRun.Background = redBtnGrad;
            btnRun.MouseEnter += (s, e) => btnRun.Background = redBtnHover;
            btnRun.MouseLeave += (s, e) => btnRun.Background = redBtnGrad;
            btnRun.Click += BtnRun_Click;
            Grid.SetColumn(btnRun, 3);
            footerGrid.Children.Add(btnRun);

            Button btnClose = new Button { Content = "Закрыть", Foreground = Brushes.White, Height = 34, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            ApplyFlatButton(btnClose, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnClose.Click += (s, e) => { DialogResult = false; Close(); };
            Grid.SetColumn(btnClose, 5);
            footerGrid.Children.Add(btnClose);

            // Start selection
            _lbCategories.SelectedIndex = 0;
        }

        private void InitializeRulesDoc()
        {
            _rulesDoc = new RulesDoc();
            foreach (var cat in CategoryRegistry.Categories)
            {
                _rulesDoc.Groups.Add(new CategoryRuleGroup
                {
                    CategoryKey = cat.CategoryKey,
                    Rules = new List<FillRule>()
                });
            }
        }

        private void LoadDefaultRules()
        {
            // Category 1: Воздуховоды
            var groupDucts = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == "OST_DuctCurves");
            if (groupDucts != null)
            {
                var rule = new FillRule { RuleName = "Оцинкованные воздуховоды t=0.5" };
                rule.Conditions.Add(new RuleCondition { ParameterName = "Имя типа", Operator = "Contains", Value = "Кругл" });
                rule.Mappings.Add(new ParamMapping { TargetParameter = "ADSK_Наименование", Formula = "Воздуховод круглый оцинкованный t=0.5 {Размер}" });
                rule.Mappings.Add(new ParamMapping { TargetParameter = "ADSK_Количество", Formula = "{Длина} * 1.1" });
                groupDucts.Rules.Add(rule);
            }

            // Category 2: Детали воздуховодов
            var groupDuctFittings = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == "OST_DuctFitting");
            if (groupDuctFittings != null)
            {
                var rule = new FillRule { RuleName = "Отводы воздуховодов" };
                rule.Conditions.Add(new RuleCondition { ParameterName = "Имя типа", Operator = "Contains", Value = "Отвод" });
                rule.Mappings.Add(new ParamMapping { TargetParameter = "ADSK_Наименование", Formula = "Отвод круглый {Угол}° {Размер}" });
                rule.Mappings.Add(new ParamMapping { TargetParameter = "ADSK_Количество", Formula = "1.0" });
                groupDuctFittings.Rules.Add(rule);
            }

            // Category 3: Трубопроводы
            var groupPipes = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == "OST_PipeCurves");
            if (groupPipes != null)
            {
                var rule = new FillRule { RuleName = "Трубы стальные" };
                rule.Conditions.Add(new RuleCondition { ParameterName = "Семейство", Operator = "Contains", Value = "Труба сталь" });
                rule.Mappings.Add(new ParamMapping { TargetParameter = "ADSK_Наименование", Formula = "Труба стальная водогазопроводная {Диаметр}x{Толщина стенки}" });
                rule.Mappings.Add(new ParamMapping { TargetParameter = "ADSK_Количество", Formula = "{Длина} * 1.05" });
                groupPipes.Rules.Add(rule);
            }
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private void LbCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCategory = _lbCategories.SelectedItem as CategoryDef;
            RefreshRulesList();
        }

        private void LbRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRule = _lbRules.SelectedItem as FillRule;
            
            bool hasSelection = _selectedRule != null;
            _btnDeleteRule.IsEnabled = hasSelection;
            _btnDuplicateRule.IsEnabled = hasSelection;
            _txtRuleName.IsEnabled = hasSelection;
            _chkRuleEnabled.IsEnabled = hasSelection;
            _tabControl.IsEnabled = hasSelection;

            if (hasSelection)
            {
                _txtRuleName.Text = _selectedRule.RuleName;
                _chkRuleEnabled.IsChecked = _selectedRule.IsEnabled;
                RefreshConditionsList();
                RefreshMappingsList();
            }
            else
            {
                _txtRuleName.Text = "";
                _chkRuleEnabled.IsChecked = true;
                _lbConditions.Items.Clear();
                _lbMappings.Items.Clear();
                ClearConditionEditor();
                ClearMappingEditor();
            }
        }

        private void RefreshRulesList()
        {
            _lbRules.Items.Clear();
            if (_selectedCategory != null)
            {
                var group = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == _selectedCategory.CategoryKey);
                if (group != null)
                {
                    foreach (var rule in group.Rules)
                    {
                        _lbRules.Items.Add(rule);
                    }
                }
            }
            _lbRules.DisplayMemberPath = "RuleName";
        }

        private void RefreshConditionsList()
        {
            _lbConditions.Items.Clear();
            if (_selectedRule != null)
            {
                foreach (var cond in _selectedRule.Conditions)
                {
                    _lbConditions.Items.Add(cond);
                }
            }
        }

        private void RefreshMappingsList()
        {
            _lbMappings.Items.Clear();
            if (_selectedRule != null)
            {
                foreach (var map in _selectedRule.Mappings)
                {
                    _lbMappings.Items.Add(map);
                }
            }
        }

        private void TxtRuleName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedRule != null)
            {
                _selectedRule.RuleName = _txtRuleName.Text;
                // Force rules listbox to update its rendering
                int idx = _lbRules.SelectedIndex;
                if (idx >= 0)
                {
                    _lbRules.Items[idx] = _selectedRule;
                    _lbRules.SelectedIndex = idx;
                }
            }
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null) return;
            var group = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == _selectedCategory.CategoryKey);
            if (group != null)
            {
                var rule = new FillRule { RuleName = "Новое правило" };
                group.Rules.Add(rule);
                RefreshRulesList();
                _lbRules.SelectedItem = rule;
            }
        }

        private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null || _selectedRule == null) return;
            var group = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == _selectedCategory.CategoryKey);
            if (group != null)
            {
                group.Rules.Remove(_selectedRule);
                RefreshRulesList();
            }
        }

        private void BtnDuplicateRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null || _selectedRule == null) return;
            var group = _rulesDoc.Groups.FirstOrDefault(g => g.CategoryKey == _selectedCategory.CategoryKey);
            if (group != null)
            {
                // Create a deep copy using simple instantiation
                var copy = new FillRule { RuleName = _selectedRule.RuleName + " - Копия", IsEnabled = _selectedRule.IsEnabled };
                foreach (var cond in _selectedRule.Conditions)
                {
                    copy.Conditions.Add(new RuleCondition { ParameterName = cond.ParameterName, Operator = cond.Operator, Value = cond.Value });
                }
                foreach (var map in _selectedRule.Mappings)
                {
                    copy.Mappings.Add(new ParamMapping { TargetParameter = map.TargetParameter, Formula = map.Formula });
                }
                group.Rules.Add(copy);
                RefreshRulesList();
                _lbRules.SelectedItem = copy;
            }
        }

        // --- CONDITIONS MANAGEMENT ---
        private RuleCondition _selectedCondition;

        private void LbConditions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCondition = _lbConditions.SelectedItem as RuleCondition;
            _btnDeleteCond.IsEnabled = _selectedCondition != null;

            if (_selectedCondition != null)
            {
                _txtCondParam.Text = _selectedCondition.ParameterName;
                _cbCondOperator.SelectedItem = _selectedCondition.Operator;
                _txtCondValue.Text = _selectedCondition.Value;
            }
            else
            {
                ClearConditionEditor();
            }
        }

        private void ClearConditionEditor()
        {
            _txtCondParam.Text = "";
            _cbCondOperator.SelectedIndex = 0;
            _txtCondValue.Text = "";
        }

        private void BtnAddCond_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var cond = new RuleCondition { ParameterName = "Имя типа", Operator = "Contains", Value = "" };
            _selectedRule.Conditions.Add(cond);
            RefreshConditionsList();
            _lbConditions.SelectedItem = cond;
        }

        private void BtnDeleteCond_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null || _selectedCondition == null) return;
            _selectedRule.Conditions.Remove(_selectedCondition);
            RefreshConditionsList();
        }

        private void TxtCondParam_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedCondition != null)
            {
                _selectedCondition.ParameterName = _txtCondParam.Text;
                UpdateConditionDisplayText();
            }
        }

        private void CbCondOperator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedCondition != null && _cbCondOperator.SelectedItem != null)
            {
                _selectedCondition.Operator = _cbCondOperator.SelectedItem.ToString();
                UpdateConditionDisplayText();
            }
        }

        private void TxtCondValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedCondition != null)
            {
                _selectedCondition.Value = _txtCondValue.Text;
                UpdateConditionDisplayText();
            }
        }

        private void UpdateConditionDisplayText()
        {
            int idx = _lbConditions.SelectedIndex;
            if (idx >= 0 && _selectedCondition != null)
            {
                _lbConditions.Items[idx] = _selectedCondition;
                _lbConditions.SelectedIndex = idx;
            }
        }

        // --- MAPPINGS MANAGEMENT ---
        private ParamMapping _selectedMapping;

        private void LbMappings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedMapping = _lbMappings.SelectedItem as ParamMapping;
            _btnDeleteMap.IsEnabled = _selectedMapping != null;

            if (_selectedMapping != null)
            {
                _txtMapParam.Text = _selectedMapping.TargetParameter;
                _txtMapFormula.Text = _selectedMapping.Formula;
            }
            else
            {
                ClearMappingEditor();
            }
        }

        private void ClearMappingEditor()
        {
            _txtMapParam.Text = "";
            _txtMapFormula.Text = "";
        }

        private void BtnAddMap_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var map = new ParamMapping { TargetParameter = "ADSK_Наименование", Formula = "" };
            _selectedRule.Mappings.Add(map);
            RefreshMappingsList();
            _lbMappings.SelectedItem = map;
        }

        private void BtnDeleteMap_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null || _selectedMapping == null) return;
            _selectedRule.Mappings.Remove(_selectedMapping);
            RefreshMappingsList();
        }

        private void TxtMapParam_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedMapping != null)
            {
                _selectedMapping.TargetParameter = _txtMapParam.Text;
                UpdateMappingDisplayText();
            }
        }

        private void TxtMapFormula_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedMapping != null)
            {
                _selectedMapping.Formula = _txtMapFormula.Text;
                UpdateMappingDisplayText();
            }
        }

        private void UpdateMappingDisplayText()
        {
            int idx = _lbMappings.SelectedIndex;
            if (idx >= 0 && _selectedMapping != null)
            {
                _lbMappings.Items[idx] = _selectedMapping;
                _lbMappings.SelectedIndex = idx;
            }
        }

        // --- IMPORT / EXPORT / RUN ---
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Файлы правил (*.json)|*.json",
                Title = "Выберите файл правил для импорта"
            };

            if (openDlg.ShowDialog() == true)
            {
                try
                {
                    using (var stream = new FileStream(openDlg.FileName, FileMode.Open))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(RulesDoc));
                        var imported = (RulesDoc)serializer.ReadObject(stream);
                        if (imported != null)
                        {
                            // Merge or replace
                            _rulesDoc = imported;
                            // Ensure all categories from CategoryRegistry are represented
                            foreach (var cat in CategoryRegistry.Categories)
                            {
                                if (!_rulesDoc.Groups.Any(g => g.CategoryKey == cat.CategoryKey))
                                {
                                    _rulesDoc.Groups.Add(new CategoryRuleGroup { CategoryKey = cat.CategoryKey });
                                }
                            }
                            RefreshRulesList();
                            MessageBox.Show("Правила успешно импортированы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось импортировать файл правил:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveDlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Файлы правил (*.json)|*.json",
                FileName = "bimbo_spec_rules.json",
                Title = "Сохранить файл правил"
            };

            if (saveDlg.ShowDialog() == true)
            {
                try
                {
                    using (var stream = new FileStream(saveDlg.FileName, FileMode.Create))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(RulesDoc));
                        serializer.WriteObject(stream, _rulesDoc);
                        MessageBox.Show("Правила успешно экспортированы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось экспортировать правила:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            IsRunRequested = true;
            DialogResult = true;
            Close();
        }

        // --- STYLES HELPER ---
        private void ApplyFlatButton(Button btn, SolidColorBrush normalBrush, SolidColorBrush hoverBrush, Style roundedStyle)
        {
            btn.Style = roundedStyle;
            btn.Background = normalBrush;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverBrush; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalBrush; };
        }

        private Style GetRoundedButtonStyle(double radius)
        {
            Style style = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

        private Style GetListBoxStyle(Brush bg, Brush border)
        {
            Style style = new Style(typeof(ListBox));
            style.Setters.Add(new Setter(ListBox.BackgroundProperty, bg));
            style.Setters.Add(new Setter(ListBox.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(ListBox.BorderBrushProperty, border));
            style.Setters.Add(new Setter(ListBox.BorderThicknessProperty, new Thickness(1)));
            return style;
        }

        private Style GetTextBoxStyle(Brush bg, Brush border)
        {
            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(TextBox.BackgroundProperty, bg));
            style.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(TextBox.BorderBrushProperty, border));
            style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(5, 0, 5, 0)));
            style.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private Style GetComboBoxStyle()
        {
            Style style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            return style;
        }
    }
}
