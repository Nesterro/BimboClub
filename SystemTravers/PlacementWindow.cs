using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class SymbolItem
    {
        public string DisplayName { get; set; }
        public object RawSymbol { get; set; }
        public override string ToString() => DisplayName;
    }

    public class PlacementState
    {
        public string SearchText { get; set; } = "";
        public string SelectedSymbolName { get; set; } = "";
        public bool ProcessDucts { get; set; } = true;
        public bool ProcessPipes { get; set; } = true;
        public bool ProcessTrays { get; set; } = true;
        public int OrientationFilterIndex { get; set; } = 0;
        public string Spacing { get; set; } = "1500";
        public string HOffset { get; set; } = "0";
        public string VOffset { get; set; } = "0";
        public bool OnlyCurrentView { get; set; } = true;
    }

    public class PlacementWindow : Window
    {
        private ComboBox _cbSymbols;
        private CheckBox _chkDucts;
        private CheckBox _chkPipes;
        private CheckBox _chkTrays;
        private TextBox _txtSpacing;
        private TextBox _txtHOffset;
        private TextBox _txtVOffset;
        private CheckBox _chkCurrentView;
        private Button _btnApply;

        private List<SymbolItem> _symbols;
        
        private TextBox _txtSearch;
        private ComboBox _cbOrientationFilter;
        private TextBlock _lblSelectedStatus;
        private Autodesk.Revit.UI.UIDocument _uidoc;
        private List<Autodesk.Revit.DB.Reference> _selectedRefs = new List<Autodesk.Revit.DB.Reference>();

        public object SelectedSymbol => ((SymbolItem)_cbSymbols.SelectedItem)?.RawSymbol;
        public bool ProcessDucts => _chkDucts.IsChecked == true;
        public bool ProcessPipes => _chkPipes.IsChecked == true;
        public bool ProcessTrays => _chkTrays.IsChecked == true;
        public double SpacingMm => ParseDouble(_txtSpacing.Text, 1500);
        public double HOffsetMm => ParseDouble(_txtHOffset.Text, 0);
        public double VOffsetMm => ParseDouble(_txtVOffset.Text, 0);
        public bool OnlyCurrentView => _chkCurrentView.IsChecked == true;
        public bool IsApplied { get; private set; } = false;
        public int OrientationFilterIndex => _cbOrientationFilter?.SelectedIndex ?? 0; // 0 = All, 1 = Horizontal, 2 = Vertical
        public List<Autodesk.Revit.DB.Reference> SelectedRefs => _selectedRefs;
        public bool SelectionRequested { get; set; } = false;

        public PlacementWindow(Autodesk.Revit.UI.UIDocument uidoc, List<SymbolItem> symbols)
        {
            _uidoc = uidoc;
            _symbols = symbols;
            InitializeComponent();
            PopulateSymbols();
        }

        private double ParseDouble(string text, double defaultVal)
        {
            if (double.TryParse(text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }
            return defaultVal;
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

        private void ApplyButtonStyles(Button btn, LinearGradientBrush normalGrad, LinearGradientBrush hoverGrad, Style roundedStyle)
        {
            btn.Style = roundedStyle;
            btn.Background = normalGrad;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverGrad; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalGrad; };
            btn.IsEnabledChanged += (s, ev) =>
            {
                if (btn.IsEnabled)
                    btn.Background = normalGrad;
                else
                    btn.Background = new SolidColorBrush(Color.FromRgb(70, 70, 82));
            };
        }

        private void ApplyFlatButtonStyles(Button btn, SolidColorBrush normalBrush, SolidColorBrush hoverBrush, Style roundedStyle)
        {
            btn.Style = roundedStyle;
            btn.Background = normalBrush;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverBrush; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalBrush; };
        }

        private void InitializeComponent()
        {
            // Настройка окна
            Title = "Расстановка по сети - BimboClub Tools";
            Width = 480;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B (Scarlet)

            // Стили
            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, controlBg));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(8, 0, 8, 0)));
            textBoxStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, controlBg));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, borderBrush));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style comboBoxItemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.BackgroundProperty, controlBg));
            comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(System.Windows.Controls.ComboBoxItem), comboBoxItemStyle);

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главный грид
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });    // 0: Шапка
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Контент
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // 2: Подвал
            Content = mainGrid;

            // 0. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(18, 18, 20), Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "РАССТАНОВКА ЭЛЕМЕНТОВ ПО СЕТИ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 1. Контент
            Grid contentGrid = new Grid { Margin = new Thickness(15, 15, 15, 10) };
            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);
            
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 0: Поиск
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 1: Семейство
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });  // 2: Типы сетей
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 3: Участки сети (фильтр ориентации)
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });  // 4: Разделитель
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 5: Шаг
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 6: Смещение Г
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 7: Смещение В
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });  // 8: Разделитель
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });  // 9: Чекбокс вида
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });  // 10: Выбор на чертеже

            // Строка 0: Поиск
            Grid rowSearch = new Grid();
            rowSearch.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowSearch.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowSearch, 0);
            contentGrid.Children.Add(rowSearch);

            TextBlock lblSearch = new TextBlock { Text = "Поиск семейства:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(lblSearch, 0);
            rowSearch.Children.Add(lblSearch);

            _txtSearch = new TextBox { Style = textBoxStyle, Height = 25, VerticalAlignment = VerticalAlignment.Center };
            _txtSearch.TextChanged += (s, e) => RefreshSymbolsFilter();
            Grid.SetColumn(_txtSearch, 1);
            rowSearch.Children.Add(_txtSearch);

            // Строка 1: Семейство
            Grid rowFamily = new Grid();
            rowFamily.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowFamily.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowFamily, 1);
            contentGrid.Children.Add(rowFamily);

            TextBlock lblFamily = new TextBlock { Text = "Выбранное семейство:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(lblFamily, 0);
            rowFamily.Children.Add(lblFamily);

            _cbSymbols = new ComboBox { Style = comboBoxStyle, Height = 25, VerticalAlignment = VerticalAlignment.Center };
            UiThemeHelper.StyleComboBoxDark(_cbSymbols, controlBg, borderBrush);
            Grid.SetColumn(_cbSymbols, 1);
            rowFamily.Children.Add(_cbSymbols);

            // Строка 2: Типы сетей
            Grid rowTypes = new Grid();
            rowTypes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowTypes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowTypes, 2);
            contentGrid.Children.Add(rowTypes);

            TextBlock lblTypes = new TextBlock { Text = "Трассировать по:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(lblTypes, 0);
            rowTypes.Children.Add(lblTypes);

            StackPanel panelTypes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(panelTypes, 1);
            rowTypes.Children.Add(panelTypes);

            _chkDucts = new CheckBox { Content = "Воздуховоды", Foreground = Brushes.White, IsChecked = true, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            _chkPipes = new CheckBox { Content = "Трубы", Foreground = Brushes.White, IsChecked = true, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            _chkTrays = new CheckBox { Content = "Лотки", Foreground = Brushes.White, IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
            panelTypes.Children.Add(_chkDucts);
            panelTypes.Children.Add(_chkPipes);
            panelTypes.Children.Add(_chkTrays);

            // Строка 3: Участки сети (фильтр ориентации)
            Grid rowOrientation = new Grid();
            rowOrientation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowOrientation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowOrientation, 3);
            contentGrid.Children.Add(rowOrientation);

            TextBlock lblOrientation = new TextBlock { Text = "Участки сети (фильтр):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(lblOrientation, 0);
            rowOrientation.Children.Add(lblOrientation);

            _cbOrientationFilter = new ComboBox { Style = comboBoxStyle, Height = 25, VerticalAlignment = VerticalAlignment.Center };
            _cbOrientationFilter.Items.Add("Все участки (гориз. и верт.)");
            _cbOrientationFilter.Items.Add("Только горизонтальные");
            _cbOrientationFilter.Items.Add("Только вертикальные");
            _cbOrientationFilter.SelectedIndex = 0;
            UiThemeHelper.StyleComboBoxDark(_cbOrientationFilter, controlBg, borderBrush);
            Grid.SetColumn(_cbOrientationFilter, 1);
            rowOrientation.Children.Add(_cbOrientationFilter);

            // Строка 5: Шаг расстановки
            Grid rowSpacing = new Grid();
            rowSpacing.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowSpacing.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowSpacing, 5);
            contentGrid.Children.Add(rowSpacing);

            TextBlock lblSpacing = new TextBlock { Text = "Шаг расстановки (мм):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblSpacing, 0);
            rowSpacing.Children.Add(lblSpacing);

            _txtSpacing = new TextBox { Style = textBoxStyle, Text = "1500", Height = 25, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_txtSpacing, 1);
            rowSpacing.Children.Add(_txtSpacing);

            // Строка 6: Смещение Г
            Grid rowHOffset = new Grid();
            rowHOffset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowHOffset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowHOffset, 6);
            contentGrid.Children.Add(rowHOffset);

            TextBlock lblHOffset = new TextBlock { Text = "Смещение по горизонтали (мм):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblHOffset, 0);
            rowHOffset.Children.Add(lblHOffset);

            _txtHOffset = new TextBox { Style = textBoxStyle, Text = "0", Height = 25, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_txtHOffset, 1);
            rowHOffset.Children.Add(_txtHOffset);

            // Строка 7: Смещение В
            Grid rowVOffset = new Grid();
            rowVOffset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rowVOffset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowVOffset, 7);
            contentGrid.Children.Add(rowVOffset);

            TextBlock lblVOffset = new TextBlock { Text = "Смещение по вертикали (мм):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblVOffset, 0);
            rowVOffset.Children.Add(lblVOffset);

            _txtVOffset = new TextBox { Style = textBoxStyle, Text = "0", Height = 25, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_txtVOffset, 1);
            rowVOffset.Children.Add(_txtVOffset);

            // Строка 9: Галочка вида
            _chkCurrentView = new CheckBox 
            { 
                Content = "Расставлять только по текущему виду", 
                Foreground = activeBorderBrush, 
                FontWeight = FontWeights.SemiBold,
                IsChecked = true, 
                VerticalAlignment = VerticalAlignment.Center 
            };
            Grid.SetRow(_chkCurrentView, 9);
            contentGrid.Children.Add(_chkCurrentView);

            // Строка 10: Выбор на чертеже
            Grid rowSelect = new Grid();
            rowSelect.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowSelect.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            Grid.SetRow(rowSelect, 10);
            contentGrid.Children.Add(rowSelect);

            _lblSelectedStatus = new TextBlock
            {
                Text = "Выбрано на чертеже: все по фильтру",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 170)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11
            };
            Grid.SetColumn(_lblSelectedStatus, 0);
            rowSelect.Children.Add(_lblSelectedStatus);

            Button btnSelectOnDrawing = new Button
            {
                Content = "Выбрать на чертеже",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 25,
                Cursor = Cursors.Hand
            };
            btnSelectOnDrawing.Click += BtnSelectOnDrawing_Click;
            ApplyFlatButtonStyles(btnSelectOnDrawing, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            Grid.SetColumn(btnSelectOnDrawing, 1);
            rowSelect.Children.Add(btnSelectOnDrawing);

            // 2. Подвал с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка Расставить
            _btnApply = new Button
            {
                Content = "Расставить",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnApply.Click += BtnApply_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnApply, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnApply, 1);
            bottomGrid.Children.Add(_btnApply);

            // Кнопка Закрыть
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 30,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            ApplyFlatButtonStyles(btnCancel, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            Grid.SetColumn(btnCancel, 2);
            bottomGrid.Children.Add(btnCancel);
        }

        private void PopulateSymbols()
        {
            _cbSymbols.Items.Clear();
            foreach (var item in _symbols)
            {
                _cbSymbols.Items.Add(item);
            }
            if (_cbSymbols.Items.Count > 0)
            {
                _cbSymbols.SelectedIndex = 0;
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSymbol == null)
            {
                MessageBox.Show("Пожалуйста, выберите размещаемое семейство.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ProcessDucts && !ProcessPipes && !ProcessTrays)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один тип сетей для расстановки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SpacingMm <= 0)
            {
                MessageBox.Show("Пожалуйста, введите положительный шаг расстановки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsApplied = true;
            DialogResult = true;
            Close();
        }

        private void RefreshSymbolsFilter()
        {
            string query = (_txtSearch.Text ?? "").Trim().ToLower();
            _cbSymbols.Items.Clear();
            foreach (var item in _symbols)
            {
                if (string.IsNullOrEmpty(query) || item.DisplayName.ToLower().Contains(query))
                {
                    _cbSymbols.Items.Add(item);
                }
            }
            if (_cbSymbols.Items.Count > 0)
            {
                _cbSymbols.SelectedIndex = 0;
            }
        }

        private void BtnSelectOnDrawing_Click(object sender, RoutedEventArgs e)
        {
            SelectionRequested = true;
            this.DialogResult = false;
            this.Close();
        }

        public void SetSelectedCount(int count)
        {
            if (count > 0)
            {
                _lblSelectedStatus.Text = $"Выбрано на чертеже: {count} шт.";
                _lblSelectedStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 46, 91));
            }
            else
            {
                _lblSelectedStatus.Text = "Выбрано на чертеже: все по фильтру";
                _lblSelectedStatus.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 170));
            }
        }

        public PlacementState SaveState()
        {
            return new PlacementState
            {
                SearchText = _txtSearch.Text,
                SelectedSymbolName = ((SymbolItem)_cbSymbols.SelectedItem)?.DisplayName ?? "",
                ProcessDucts = _chkDucts.IsChecked == true,
                ProcessPipes = _chkPipes.IsChecked == true,
                ProcessTrays = _chkTrays.IsChecked == true,
                OrientationFilterIndex = _cbOrientationFilter.SelectedIndex,
                Spacing = _txtSpacing.Text,
                HOffset = _txtHOffset.Text,
                VOffset = _txtVOffset.Text,
                OnlyCurrentView = _chkCurrentView.IsChecked == true
            };
        }

        public void LoadState(PlacementState state)
        {
            if (state == null) return;
            _txtSearch.Text = state.SearchText;
            
            RefreshSymbolsFilter();
            
            if (!string.IsNullOrEmpty(state.SelectedSymbolName))
            {
                for (int i = 0; i < _cbSymbols.Items.Count; i++)
                {
                    if (((SymbolItem)_cbSymbols.Items[i]).DisplayName == state.SelectedSymbolName)
                    {
                        _cbSymbols.SelectedIndex = i;
                        break;
                    }
                }
            }
            _chkDucts.IsChecked = state.ProcessDucts;
            _chkPipes.IsChecked = state.ProcessPipes;
            _chkTrays.IsChecked = state.ProcessTrays;
            if (state.OrientationFilterIndex >= 0 && state.OrientationFilterIndex < _cbOrientationFilter.Items.Count)
            {
                _cbOrientationFilter.SelectedIndex = state.OrientationFilterIndex;
            }
            _txtSpacing.Text = state.Spacing;
            _txtHOffset.Text = state.HOffset;
            _txtVOffset.Text = state.VOffset;
            _chkCurrentView.IsChecked = state.OnlyCurrentView;
        }
    }
}
