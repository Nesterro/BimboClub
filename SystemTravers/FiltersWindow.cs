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
    public class ViewItem
    {
        public string Name { get; set; }
        public object RawView { get; set; }
        public override string ToString() => Name;
    }

    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isChecked = true;
        public string Name { get; set; }
        public object RawFilter { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TargetViewItem : INotifyPropertyChanged
    {
        private bool _isChecked = false;
        public string Name { get; set; }
        public string ViewType { get; set; }
        public object RawView { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FiltersWindow : Window
    {
        private ComboBox _cbSourceView;
        private ListBox _lstFilters;
        private ListBox _lstTargetViews;
        private TextBox _searchBox;
        private Button _btnCopy;

        private List<ViewItem> _sourceViews;
        private List<FilterItem> _allFilters;
        private List<TargetViewItem> _allTargetViews;
        private Func<object, List<FilterItem>> _onSourceViewChangedCallback;

        public object SelectedSourceView => ((ViewItem)_cbSourceView.SelectedItem)?.RawView;
        public List<FilterItem> SelectedFilters => _allFilters.Where(f => f.IsChecked).ToList();
        public List<TargetViewItem> SelectedTargetViews => _allTargetViews.Where(t => t.IsChecked).ToList();
        public bool IsCopyRequested { get; private set; } = false;

        public FiltersWindow(
            List<ViewItem> sourceViews, 
            List<TargetViewItem> targetViews, 
            object activeView,
            Func<object, List<FilterItem>> onSourceViewChangedCallback)
        {
            _sourceViews = sourceViews;
            _allTargetViews = targetViews;
            _onSourceViewChangedCallback = onSourceViewChangedCallback;
            _allFilters = new List<FilterItem>();

            InitializeComponent();
            PopulateSourceViews(activeView);
            RefreshTargetViewsList();
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
            Title = "Копирование фильтров видов - BimboClub Tools";
            Width = 560;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            MinHeight = 600;
            MinWidth = 450;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B (Scarlet)
            SolidColorBrush textMuted = new SolidColorBrush(Color.FromRgb(160, 160, 170));

            // Стили
            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, controlBg));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(5, 0, 5, 0)));
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // 0: Шапка
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(75) });    // 1: Исходный вид
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(170) });   // 2: Фильтры (ListBox + кнопки)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });    // 3: Поиск целевых видов
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 4: Целевые виды
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });    // 5: Подвал
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
                Text = "ПЕРЕНОС ФИЛЬТРОВ И ГРАФИКИ ВИДОВ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 1. Исходный вид
            Grid sourceGrid = new Grid { Margin = new Thickness(15, 8, 15, 5) };
            Grid.SetRow(sourceGrid, 1);
            mainGrid.Children.Add(sourceGrid);
            sourceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            sourceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

            TextBlock lblSource = new TextBlock { Text = "Исходный вид (донор фильтров):", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, FontSize = 11 };
            Grid.SetRow(lblSource, 0);
            sourceGrid.Children.Add(lblSource);

            _cbSourceView = new ComboBox { Style = comboBoxStyle, Height = 25, VerticalAlignment = VerticalAlignment.Center };
            UiThemeHelper.StyleComboBoxDark(_cbSourceView, controlBg, borderBrush);
            _cbSourceView.SelectionChanged += CbSourceView_SelectionChanged;
            Grid.SetRow(_cbSourceView, 1);
            sourceGrid.Children.Add(_cbSourceView);

            // 2. Фильтры
            Grid filtersGrid = new Grid { Margin = new Thickness(15, 5, 15, 5) };
            Grid.SetRow(filtersGrid, 2);
            mainGrid.Children.Add(filtersGrid);
            filtersGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            filtersGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            filtersGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

            TextBlock lblFilters = new TextBlock { Text = "ВЫБЕРИТЕ ФИЛЬТРЫ ДЛЯ КОПИРОВАНИЯ:", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
            Grid.SetRow(lblFilters, 0);
            filtersGrid.Children.Add(lblFilters);

            Border listFiltersBorder = new Border { Background = panelBg, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(listFiltersBorder, 1);
            filtersGrid.Children.Add(listFiltersBorder);

            _lstFilters = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            listFiltersBorder.Child = _lstFilters;

            StackPanel btnFiltersStack = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetRow(btnFiltersStack, 2);
            filtersGrid.Children.Add(btnFiltersStack);

            Button btnSelectAllFilters = new Button { Content = "Выбрать все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Width = 110, Height = 26, FontSize = 11, Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnSelectAllFilters, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnSelectAllFilters.Click += (s, e) => SetFiltersSelection(true);
            btnFiltersStack.Children.Add(btnSelectAllFilters);

            Button btnDeselectAllFilters = new Button { Content = "Снять все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Width = 110, Height = 26, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnDeselectAllFilters, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnDeselectAllFilters.Click += (s, e) => SetFiltersSelection(false);
            btnFiltersStack.Children.Add(btnDeselectAllFilters);

            // 3. Поиск целевых видов
            Grid searchGrid = new Grid { Margin = new Thickness(15, 5, 15, 5) };
            Grid.SetRow(searchGrid, 3);
            mainGrid.Children.Add(searchGrid);
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblSearch = new TextBlock { Text = "Поиск целевых видов:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblSearch, 0);
            searchGrid.Children.Add(lblSearch);

            _searchBox = new TextBox { Style = textBoxStyle, Height = 24, VerticalAlignment = VerticalAlignment.Center };
            _searchBox.TextChanged += (s, e) => RefreshTargetViewsList();
            Grid.SetColumn(_searchBox, 1);
            searchGrid.Children.Add(_searchBox);

            // 4. Целевые виды
            Grid targetsGrid = new Grid { Margin = new Thickness(15, 5, 15, 5) };
            Grid.SetRow(targetsGrid, 4);
            mainGrid.Children.Add(targetsGrid);
            targetsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            targetsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            targetsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

            TextBlock lblTargets = new TextBlock { Text = "ВЫБЕРИТЕ ЦЕЛЕВЫЕ ВИДЫ (ПОЛУЧАТЕЛИ ФИЛЬТРОВ):", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
            Grid.SetRow(lblTargets, 0);
            targetsGrid.Children.Add(lblTargets);

            Border listTargetsBorder = new Border { Background = panelBg, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(listTargetsBorder, 1);
            targetsGrid.Children.Add(listTargetsBorder);

            _lstTargetViews = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            listTargetsBorder.Child = _lstTargetViews;

            StackPanel btnTargetsStack = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetRow(btnTargetsStack, 2);
            targetsGrid.Children.Add(btnTargetsStack);

            Button btnSelectAllTargets = new Button { Content = "Выбрать все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Width = 110, Height = 26, FontSize = 11, Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnSelectAllTargets, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnSelectAllTargets.Click += (s, e) => SetTargetsSelection(true);
            btnTargetsStack.Children.Add(btnSelectAllTargets);

            Button btnDeselectAllTargets = new Button { Content = "Снять все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Width = 110, Height = 26, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnDeselectAllTargets, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnDeselectAllTargets.Click += (s, e) => SetTargetsSelection(false);
            btnTargetsStack.Children.Add(btnDeselectAllTargets);

            // 5. Подвал с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 5);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка запуска
            _btnCopy = new Button
            {
                Content = "Скопировать фильтры",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnCopy.Click += BtnCopy_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnCopy, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnCopy, 1);
            bottomGrid.Children.Add(_btnCopy);

            // Кнопка Закрыть
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            ApplyFlatButtonStyles(btnCancel, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            Grid.SetColumn(btnCancel, 2);
            bottomGrid.Children.Add(btnCancel);
        }

        private void PopulateSourceViews(object activeView)
        {
            _cbSourceView.Items.Clear();
            ViewItem selectedItem = null;

            foreach (var viewItem in _sourceViews)
            {
                _cbSourceView.Items.Add(viewItem);
                if (activeView != null && viewItem.RawView == activeView)
                {
                    selectedItem = viewItem;
                }
            }

            if (selectedItem != null)
            {
                _cbSourceView.SelectedItem = selectedItem;
            }
            else if (_cbSourceView.Items.Count > 0)
            {
                _cbSourceView.SelectedIndex = 0;
            }
        }

        private void CbSourceView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = SelectedSourceView;
            if (selected == null || _onSourceViewChangedCallback == null) return;

            _allFilters = _onSourceViewChangedCallback(selected);
            RefreshFiltersList();
        }

        private void RefreshFiltersList()
        {
            _lstFilters.Items.Clear();
            foreach (var item in _allFilters)
            {
                Grid row = new Grid { Margin = new Thickness(5, 2, 5, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                CheckBox chk = new CheckBox { VerticalAlignment = VerticalAlignment.Center, IsChecked = item.IsChecked };
                chk.Checked += (s, e) => item.IsChecked = true;
                chk.Unchecked += (s, e) => item.IsChecked = false;
                Grid.SetColumn(chk, 0);
                row.Children.Add(chk);

                TextBlock txtName = new TextBlock { Text = item.Name, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(txtName, 1);
                row.Children.Add(txtName);

                _lstFilters.Items.Add(row);
            }
        }

        private void RefreshTargetViewsList()
        {
            if (_lstTargetViews == null) return;

            _lstTargetViews.Items.Clear();
            string search = _searchBox?.Text ?? "";

            var filtered = _allTargetViews;
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = _allTargetViews.Where(t => t.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            foreach (var item in filtered)
            {
                Grid row = new Grid { Margin = new Thickness(5, 2, 5, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Тип вида

                CheckBox chk = new CheckBox { VerticalAlignment = VerticalAlignment.Center, IsChecked = item.IsChecked };
                chk.Checked += (s, e) => item.IsChecked = true;
                chk.Unchecked += (s, e) => item.IsChecked = false;
                Grid.SetColumn(chk, 0);
                row.Children.Add(chk);

                TextBlock txtName = new TextBlock { Text = item.Name, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(txtName, 1);
                row.Children.Add(txtName);

                Border typeBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(48, 48, 56)), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                TextBlock txtType = new TextBlock { Text = item.ViewType, Foreground = Brushes.Gray, FontSize = 10, FontWeight = FontWeights.SemiBold };
                typeBorder.Child = txtType;
                Grid.SetColumn(typeBorder, 2);
                row.Children.Add(typeBorder);

                _lstTargetViews.Items.Add(row);
            }
        }

        private void SetFiltersSelection(bool select)
        {
            foreach (var f in _allFilters) f.IsChecked = select;
            RefreshFiltersList();
        }

        private void SetTargetsSelection(bool select)
        {
            // Выделяем/снимаем выделение только с отфильтрованных в данный момент видов
            string search = _searchBox?.Text ?? "";
            var targets = _allTargetViews;
            if (!string.IsNullOrWhiteSpace(search))
            {
                targets = _allTargetViews.Where(t => t.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            foreach (var t in targets) t.IsChecked = select;
            RefreshTargetViewsList();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSourceView == null)
            {
                MessageBox.Show("Пожалуйста, выберите исходный вид (донор).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedFilters.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один фильтр для копирования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedTargetViews.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один целевой вид (получатель).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsCopyRequested = true;
            DialogResult = true;
            Close();
        }
    }
}
