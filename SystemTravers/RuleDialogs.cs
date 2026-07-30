using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BimboClub.RuleEditor
{
    // --- 1. ВЫБОР ПАРАМЕТРА (С ПОИСКОМ) ---
    public class SelectParameterWindow : Window
    {
        public string SelectedParameterName { get; private set; }
        private ListBox _lbParams;
        private TextBox _txtSearch;
        private List<string> _allParams;

        public SelectParameterWindow(List<string> availableParams)
        {
            _allParams = availableParams ?? new List<string>();
            Title = "Выбор параметра - BimboClub";
            Width = 420;
            Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            UiThemeHelper.ApplyDarkTheme(this);

            Grid grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Search Box
            _txtSearch = new TextBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 58)),
                Padding = new Thickness(8, 6, 8, 6),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _txtSearch.TextChanged += (s, e) => FilterList();
            Grid.SetRow(_txtSearch, 0);
            grid.Children.Add(_txtSearch);

            // ListBox
            _lbParams = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 35)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            _lbParams.MouseDoubleClick += (s, e) => { OnOkClick(); };
            Grid.SetRow(_lbParams, 1);
            grid.Children.Add(_lbParams);

            // Buttons Footer
            StackPanel btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button btnOk = CreateButton("Выбрать", true);
            btnOk.Click += (s, e) => OnOkClick();
            Button btnCancel = CreateButton("Отмена", false);
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            Content = grid;
            FilterList();
        }

        private void FilterList()
        {
            string query = _txtSearch.Text?.Trim().ToLower() ?? "";
            var filtered = string.IsNullOrEmpty(query)
                ? _allParams
                : _allParams.Where(p => p.ToLower().Contains(query)).ToList();

            _lbParams.ItemsSource = filtered;
            if (filtered.Count > 0) _lbParams.SelectedIndex = 0;
        }

        private void OnOkClick()
        {
            if (_lbParams.SelectedItem != null)
            {
                SelectedParameterName = _lbParams.SelectedItem.ToString();
                DialogResult = true;
                Close();
            }
            else if (!string.IsNullOrWhiteSpace(_txtSearch.Text))
            {
                SelectedParameterName = _txtSearch.Text.Trim();
                DialogResult = true;
                Close();
            }
        }

        private Button CreateButton(string text, bool isPrimary)
        {
            Button b = new Button
            {
                Content = text,
                Width = 90,
                Height = 32,
                Margin = new Thickness(5, 0, 0, 0),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            if (isPrimary)
            {
                b.Background = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            }
            else
            {
                b.Background = new SolidColorBrush(Color.FromRgb(45, 45, 52));
            }
            return b;
        }
    }

    // --- 2. РЕДАКТИРОВАНИЕ УСЛОВИЯ (ConditionEditWindow) ---
    public class ConditionEditWindow : Window
    {
        public RuleConditionModel Condition { get; private set; }
        private List<string> _availableParams;

        private ComboBox _cbMode;
        private TextBox _txtParamName;
        private ComboBox _cbOperator;
        private TextBox _txtValue;

        public ConditionEditWindow(RuleConditionModel existing, List<string> availableParams)
        {
            Condition = existing ?? new RuleConditionModel();
            _availableParams = availableParams ?? new List<string>();

            Title = "Условие правила - BimboClub";
            Width = 460;
            Height = 340;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            UiThemeHelper.ApplyDarkTheme(this);

            StackPanel mainPanel = new StackPanel { Margin = new Thickness(20) };

            // 1. Поиск режима (Self, Type, Host, Parent)
            mainPanel.Children.Add(CreateLabel("Источник параметра:"));
            _cbMode = new ComboBox
            {
                Height = 30,
                Margin = new Thickness(0, 4, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White
            };
            _cbMode.ItemsSource = Enum.GetValues(typeof(ParamGettingMode));
            _cbMode.SelectedItem = Condition.Mode;
            mainPanel.Children.Add(_cbMode);

            // 2. Имя параметра + кнопка выбора
            mainPanel.Children.Add(CreateLabel("Параметр элемента:"));
            Grid paramGrid = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtParamName = new TextBox
            {
                Height = 30,
                Text = Condition.ParameterName ?? "",
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                Padding = new Thickness(6, 4, 6, 4)
            };
            Grid.SetColumn(_txtParamName, 0);
            paramGrid.Children.Add(_txtParamName);

            Button btnSelectParam = new Button
            {
                Content = "...",
                Width = 35,
                Height = 30,
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White
            };
            btnSelectParam.Click += (s, e) =>
            {
                SelectParameterWindow dlg = new SelectParameterWindow(_availableParams);
                if (dlg.ShowDialog() == true)
                {
                    _txtParamName.Text = dlg.SelectedParameterName;
                }
            };
            Grid.SetColumn(btnSelectParam, 1);
            paramGrid.Children.Add(btnSelectParam);
            mainPanel.Children.Add(paramGrid);

            // 3. Оператор сравнения
            mainPanel.Children.Add(CreateLabel("Оператор сравнения:"));
            _cbOperator = new ComboBox
            {
                Height = 30,
                Margin = new Thickness(0, 4, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White
            };
            _cbOperator.ItemsSource = Enum.GetValues(typeof(ConditionOperator));
            _cbOperator.SelectedItem = Condition.Operator;
            mainPanel.Children.Add(_cbOperator);

            // 4. Значение
            mainPanel.Children.Add(CreateLabel("Сравниваемое значение:"));
            _txtValue = new TextBox
            {
                Height = 30,
                Text = Condition.Value ?? "",
                Margin = new Thickness(0, 4, 0, 20),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                Padding = new Thickness(6, 4, 6, 4)
            };
            mainPanel.Children.Add(_txtValue);

            // Buttons
            StackPanel btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Button btnSave = new Button
            {
                Content = "Сохранить",
                Width = 100,
                Height = 32,
                Background = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            btnSave.Click += (s, e) =>
            {
                Condition.Mode = (ParamGettingMode)_cbMode.SelectedItem;
                Condition.ParameterName = _txtParamName.Text.Trim();
                Condition.Operator = (ConditionOperator)_cbOperator.SelectedItem;
                Condition.Value = _txtValue.Text.Trim();
                DialogResult = true;
                Close();
            };
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Width = 90,
                Height = 32,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            mainPanel.Children.Add(btnPanel);

            Content = mainPanel;
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
        }
    }

    // --- 3. РЕДАКТИРОВАНИЕ ДЕЙСТВИЯ (ActionEditWindow) ---
    public class ActionEditWindow : Window
    {
        public RuleActionModel ActionModel { get; private set; }
        private List<string> _availableParams;

        private TextBox _txtTargetParam;
        private ComboBox _cbActionType;
        private ComboBox _cbSourceMode;
        private TextBox _txtSourceParam;
        private TextBox _txtConstant;
        private TextBox _txtPrefix;
        private TextBox _txtSuffix;

        public ActionEditWindow(RuleActionModel existing, List<string> availableParams)
        {
            ActionModel = existing ?? new RuleActionModel();
            _availableParams = availableParams ?? new List<string>();

            Title = "Действие / Заполнение параметра - BimboClub";
            Width = 500;
            Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            UiThemeHelper.ApplyDarkTheme(this);

            StackPanel mainPanel = new StackPanel { Margin = new Thickness(20) };

            // 1. Целевой параметр
            mainPanel.Children.Add(CreateLabel("Целевой параметр для записи:"));
            Grid targetGrid = CreateParamSelectGrid(ActionModel.TargetParameter, out _txtTargetParam);
            mainPanel.Children.Add(targetGrid);

            // 2. Тип действия
            mainPanel.Children.Add(CreateLabel("Способ формирования значения:"));
            _cbActionType = new ComboBox
            {
                Height = 30,
                Margin = new Thickness(0, 4, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White
            };
            _cbActionType.ItemsSource = Enum.GetValues(typeof(ActionType));
            _cbActionType.SelectedItem = ActionModel.ActionType;
            mainPanel.Children.Add(_cbActionType);

            // 3. Режим и источник
            mainPanel.Children.Add(CreateLabel("Источник считывания:"));
            _cbSourceMode = new ComboBox
            {
                Height = 30,
                Margin = new Thickness(0, 4, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White
            };
            _cbSourceMode.ItemsSource = Enum.GetValues(typeof(ParamGettingMode));
            _cbSourceMode.SelectedItem = ActionModel.SourceMode;
            mainPanel.Children.Add(_cbSourceMode);

            mainPanel.Children.Add(CreateLabel("Исходный параметр / Константа:"));
            Grid sourceGrid = CreateParamSelectGrid(ActionModel.SourceParameter, out _txtSourceParam);
            mainPanel.Children.Add(sourceGrid);

            // 4. Префикс и Суффикс
            Grid prefixSuffixGrid = new Grid { Margin = new Thickness(0, 4, 0, 15) };
            prefixSuffixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            prefixSuffixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            prefixSuffixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel pnlPref = new StackPanel();
            pnlPref.Children.Add(CreateLabel("Префикс:"));
            _txtPrefix = new TextBox
            {
                Height = 28,
                Text = ActionModel.Prefix ?? "",
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 0)
            };
            pnlPref.Children.Add(_txtPrefix);
            Grid.SetColumn(pnlPref, 0);
            prefixSuffixGrid.Children.Add(pnlPref);

            StackPanel pnlSuff = new StackPanel();
            pnlSuff.Children.Add(CreateLabel("Суффикс:"));
            _txtSuffix = new TextBox
            {
                Height = 28,
                Text = ActionModel.Suffix ?? "",
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 0)
            };
            pnlSuff.Children.Add(_txtSuffix);
            Grid.SetColumn(pnlSuff, 2);
            prefixSuffixGrid.Children.Add(pnlSuff);

            mainPanel.Children.Add(prefixSuffixGrid);

            // Buttons
            StackPanel btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Button btnSave = new Button
            {
                Content = "Сохранить",
                Width = 100,
                Height = 32,
                Background = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            btnSave.Click += (s, e) =>
            {
                ActionModel.TargetParameter = _txtTargetParam.Text.Trim();
                ActionModel.ActionType = (ActionType)_cbActionType.SelectedItem;
                ActionModel.SourceMode = (ParamGettingMode)_cbSourceMode.SelectedItem;
                ActionModel.SourceParameter = _txtSourceParam.Text.Trim();
                ActionModel.Prefix = _txtPrefix.Text;
                ActionModel.Suffix = _txtSuffix.Text;
                DialogResult = true;
                Close();
            };
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Width = 90,
                Height = 32,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            mainPanel.Children.Add(btnPanel);

            Content = mainPanel;
        }

        private Grid CreateParamSelectGrid(string initialText, out TextBox txtBox)
        {
            Grid grid = new Grid { Margin = new Thickness(0, 4, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtBox = new TextBox
            {
                Height = 30,
                Text = initialText ?? "",
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                Padding = new Thickness(6, 4, 6, 4)
            };
            Grid.SetColumn(txtBox, 0);
            grid.Children.Add(txtBox);

            TextBox localTb = txtBox;
            Button btnSelectParam = new Button
            {
                Content = "...",
                Width = 35,
                Height = 30,
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White
            };
            btnSelectParam.Click += (s, e) =>
            {
                SelectParameterWindow dlg = new SelectParameterWindow(_availableParams);
                if (dlg.ShowDialog() == true)
                {
                    localTb.Text = dlg.SelectedParameterName;
                }
            };
            Grid.SetColumn(btnSelectParam, 1);
            grid.Children.Add(btnSelectParam);

            return grid;
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
        }
    }
}
