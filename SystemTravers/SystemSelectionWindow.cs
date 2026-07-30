using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ElementId = Autodesk.Revit.DB.ElementId;
using BuiltInParameter = Autodesk.Revit.DB.BuiltInParameter;

namespace BimboClub
{
    public class SystemItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        public string Name { get; set; }
        public int ElementCount { get; set; }
        public string TypeName { get; set; }
        public object SystemObj { get; set; }

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

    public class ViewParameterConfig
    {
        public bool IsEnabled { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class FilterParameterItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }

    public class SystemSelectionWindow : Window
    {
        private List<SystemItem> _allSystems;
        private List<FilterParameterItem> _allFilterParams;
        private ListBox _listBox;
        private TextBox _searchBox;
        private Button _btnGenerate;

        private ComboBox _cbFilterParam;
        private TextBox _txtFilterParamSearch;

        private TextBox _txtPrefix;
        private TextBox _txtSuffix;
        private CheckBox _chkParam1;
        private ComboBox _cbParam1;
        private TextBox _txtParamVal1;
        private CheckBox _chkParam2;
        private ComboBox _cbParam2;
        private TextBox _txtParamVal2;

        public string ViewPrefix { get; private set; } = "3D_";
        public string ViewSuffix { get; private set; } = "";
        public List<ViewParameterConfig> ViewParameters { get; private set; } = new List<ViewParameterConfig>();
        public ElementId SelectedFilterParamId { get; private set; }

        public List<SystemItem> SelectedSystems => _allSystems.Where(s => s.IsChecked).ToList();

        public SystemSelectionWindow(List<SystemItem> systems, List<FilterParameterItem> filterParams)
        {
            _allSystems = systems;
            _allFilterParams = filterParams;
            InitializeComponent();
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
            this.Title = "Выбор инженерных систем - BimboClub Tools";
            this.Width = 500;
            this.Height = 730;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            this.FontFamily = new FontFamily("Segoe UI");
            this.FontSize = 13;
            this.MinHeight = 500;
            this.MinWidth = 450;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B (Scarlet)
            SolidColorBrush textMuted = new SolidColorBrush(Color.FromRgb(160, 160, 170));

            // Общие стили для элементов ввода
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
            comboBoxStyle.Setters.Add(new Setter(ComboBox.HeightProperty, 24.0));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style comboBoxItemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.BackgroundProperty, controlBg));
            comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(System.Windows.Controls.ComboBoxItem), comboBoxItemStyle);

            Style checkBoxStyle = new Style(typeof(CheckBox));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brushes.White));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center));

            // Получаем скругленный стиль кнопки с CornerRadius = 5
            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главный грид
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // 0: Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });    // 1: Целевой параметр
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });    // 2: Поиск
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3: Список
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });    // 4: Быстрый выбор
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });   // 5: Настройки вида и параметров
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // 6: Кнопки
            this.Content = mainGrid;

            // 1. Заголовок (Градиент от черного к алому с мягким свечением)
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 18, 20),
                    Color.FromRgb(179, 14, 45), // #B30E2D (Бренд алый)
                    0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "ВЫБОР СИСТЕМ ДЛЯ 3D СХЕМ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Поиск
            Grid searchGrid = new Grid { Margin = new Thickness(15, 5, 15, 5) };
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(searchGrid, 2);
            mainGrid.Children.Add(searchGrid);

            _searchBox = new TextBox
            {
                Style = textBoxStyle,
                BorderBrush = borderBrush
            };
            
            // Плейсхолдер для поиска (имитация)
            _searchBox.Text = "Поиск систем...";
            _searchBox.Foreground = Brushes.Gray;
            _searchBox.GotFocus += (s, e) => 
            { 
                _searchBox.BorderBrush = activeBorderBrush;
                if (_searchBox.Text == "Поиск систем...") 
                { 
                    _searchBox.Text = ""; 
                    _searchBox.Foreground = Brushes.White; 
                } 
            };
            _searchBox.LostFocus += (s, e) => 
            { 
                _searchBox.BorderBrush = borderBrush;
                if (string.IsNullOrWhiteSpace(_searchBox.Text)) 
                { 
                    _searchBox.Text = "Поиск систем..."; 
                    _searchBox.Foreground = Brushes.Gray; 
                } 
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            searchGrid.Children.Add(_searchBox);

            // 3. Список систем
            Border listBorder = new Border
            {
                Margin = new Thickness(15, 5, 15, 5),
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetRow(listBorder, 3);
            mainGrid.Children.Add(listBorder);

            _listBox = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            listBorder.Child = _listBox;

            // Шаблон элемента списка
            DataTemplate itemTemplate = new DataTemplate();
            FrameworkElementFactory gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.MarginProperty, new Thickness(5, 5, 5, 5));

            FrameworkElementFactory col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            FrameworkElementFactory col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(90));
            
            gridFactory.AppendChild(col1);
            gridFactory.AppendChild(col2);

            // CheckBox + Имя системы
            FrameworkElementFactory chk = new FrameworkElementFactory(typeof(CheckBox));
            chk.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Mode = BindingMode.TwoWay });
            chk.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chk.SetValue(CheckBox.ForegroundProperty, Brushes.White);
            
            FrameworkElementFactory textBlockName = new FrameworkElementFactory(typeof(TextBlock));
            textBlockName.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            textBlockName.SetValue(TextBlock.MarginProperty, new Thickness(25, 0, 0, 0));
            textBlockName.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            textBlockName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            textBlockName.SetValue(TextBlock.ForegroundProperty, Brushes.White); // Белый текст в темной теме!

            gridFactory.AppendChild(chk);
            gridFactory.AppendChild(textBlockName);

            // Кол-во элементов
            FrameworkElementFactory textBlockCount = new FrameworkElementFactory(typeof(TextBlock));
            textBlockCount.SetBinding(TextBlock.TextProperty, new Binding("ElementCount") { StringFormat = "({0} эл.)" });
            textBlockCount.SetValue(TextBlock.ForegroundProperty, textMuted);
            textBlockCount.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            textBlockCount.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlockCount.SetValue(Grid.ColumnProperty, 1);
            gridFactory.AppendChild(textBlockCount);

            itemTemplate.VisualTree = gridFactory;
            _listBox.ItemTemplate = itemTemplate;
            _listBox.ItemsSource = _allSystems;

            // 4. Быстрый выбор
            Grid selectGrid = new Grid { Margin = new Thickness(15, 0, 15, 0) };
            selectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(selectGrid, 4);
            mainGrid.Children.Add(selectGrid);

            Button btnSelectAll = new Button
            {
                Content = "Выбрать все",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = activeBorderBrush,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontWeight = FontWeights.SemiBold
            };
            btnSelectAll.Click += (s, e) => SetAllChecked(true);
            selectGrid.Children.Add(btnSelectAll);

            Button btnSelectNone = new Button
            {
                Content = "Снять выделение",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = activeBorderBrush,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = FontWeights.SemiBold
            };
            btnSelectNone.Click += (s, e) => SetAllChecked(false);
            Grid.SetColumn(btnSelectNone, 1);
            selectGrid.Children.Add(btnSelectNone);

            // 4.5. Настройки вида и параметров (Премиальная темная карточка)
            Border settingsBorder = new Border
            {
                Margin = new Thickness(15, 0, 15, 10),
                Padding = new Thickness(12),
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetRow(settingsBorder, 5);
            mainGrid.Children.Add(settingsBorder);

            Grid settingsGrid = new Grid();
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Заголовок раздела
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Префикс / Суффикс
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Параметр 1
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Параметр 2
            settingsBorder.Child = settingsGrid;

            // Заголовок раздела
            TextBlock txtSectionTitle = new TextBlock
            {
                Text = "Параметры создаваемых видов:",
                FontWeight = FontWeights.Bold,
                Foreground = activeBorderBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(txtSectionTitle, 0);
            settingsGrid.Children.Add(txtSectionTitle);

            // Целевой параметр для фильтра (размещаем перед списком систем)
            Grid filterParamGrid = new Grid { Margin = new Thickness(15, 5, 15, 5) };
            Grid.SetRow(filterParamGrid, 1);
            mainGrid.Children.Add(filterParamGrid);
            filterParamGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) }); // Label
            filterParamGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // ComboBox
            filterParamGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Search Box

            TextBlock lblFilterParam = new TextBlock { Text = "Параметр фильтра:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblFilterParam, 0);
            filterParamGrid.Children.Add(lblFilterParam);

            _cbFilterParam = new ComboBox
            {
                Style = comboBoxStyle,
                Height = 24,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(_cbFilterParam, 1);
            filterParamGrid.Children.Add(_cbFilterParam);
            _cbFilterParam.ItemsSource = _allFilterParams;

            // Выбираем по умолчанию "Имя системы" (RBS_SYSTEM_NAME_PARAM)
            var defaultParam = _allFilterParams.FirstOrDefault(p => p.Id.IntegerValue == (int)Autodesk.Revit.DB.BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            if (defaultParam != null)
            {
                _cbFilterParam.SelectedItem = defaultParam;
            }
            else if (_allFilterParams.Count > 0)
            {
                _cbFilterParam.SelectedIndex = 0;
            }

            _txtFilterParamSearch = new TextBox
            {
                Style = textBoxStyle,
                Height = 24,
                Text = "Поиск..."
            };
            _txtFilterParamSearch.Foreground = Brushes.Gray;
            _txtFilterParamSearch.GotFocus += (s, e) =>
            {
                if (_txtFilterParamSearch.Text == "Поиск...")
                {
                    _txtFilterParamSearch.Text = "";
                    _txtFilterParamSearch.Foreground = Brushes.White;
                }
            };
            _txtFilterParamSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtFilterParamSearch.Text))
                {
                    _txtFilterParamSearch.Text = "Поиск...";
                    _txtFilterParamSearch.Foreground = Brushes.Gray;
                }
            };
            _txtFilterParamSearch.TextChanged += FilterParamSearch_TextChanged;
            Grid.SetColumn(_txtFilterParamSearch, 2);
            filterParamGrid.Children.Add(_txtFilterParamSearch);

            // Префикс / Суффикс
            Grid nameGrid = new Grid();
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(nameGrid, 1);
            settingsGrid.Children.Add(nameGrid);

            TextBlock lblPrefix = new TextBlock { Text = "Префикс:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblPrefix, 0);
            nameGrid.Children.Add(lblPrefix);

            _txtPrefix = new TextBox
            {
                Style = textBoxStyle,
                Text = "",
                Height = 24
            };
            Grid.SetColumn(_txtPrefix, 1);
            nameGrid.Children.Add(_txtPrefix);

            TextBlock lblSuffix = new TextBlock { Text = "Суффикс:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblSuffix, 3);
            nameGrid.Children.Add(lblSuffix);

            _txtSuffix = new TextBox
            {
                Style = textBoxStyle,
                Text = "",
                Height = 24
            };
            Grid.SetColumn(_txtSuffix, 4);
            nameGrid.Children.Add(_txtSuffix);

            // Параметр 1
            Grid param1Grid = new Grid();
            param1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            param1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            param1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            param1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(param1Grid, 2);
            settingsGrid.Children.Add(param1Grid);

            _chkParam1 = new CheckBox { Style = checkBoxStyle, IsChecked = true };
            Grid.SetColumn(_chkParam1, 0);
            param1Grid.Children.Add(_chkParam1);

            _cbParam1 = new ComboBox
            {
                Style = comboBoxStyle,
                IsEditable = true
            };
            _cbParam1.Items.Add("ADSK_Назначение вида");
            _cbParam1.Items.Add("Назначение вида");
            _cbParam1.Items.Add("Раздел проекта");
            _cbParam1.Text = "ADSK_Назначение вида";
            Grid.SetColumn(_cbParam1, 1);
            param1Grid.Children.Add(_cbParam1);

            _txtParamVal1 = new TextBox
            {
                Style = textBoxStyle,
                Text = "3D Схемы",
                Height = 24
            };
            Grid.SetColumn(_txtParamVal1, 3);
            param1Grid.Children.Add(_txtParamVal1);

            // Параметр 2
            Grid param2Grid = new Grid();
            param2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            param2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            param2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            param2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(param2Grid, 3);
            settingsGrid.Children.Add(param2Grid);

            _chkParam2 = new CheckBox { Style = checkBoxStyle, IsChecked = true };
            Grid.SetColumn(_chkParam2, 0);
            param2Grid.Children.Add(_chkParam2);

            _cbParam2 = new ComboBox
            {
                Style = comboBoxStyle,
                IsEditable = true
            };
            _cbParam2.Items.Add("ADSK_Штамп Раздел проекта");
            _cbParam2.Items.Add("Раздел проекта");
            _cbParam2.Items.Add("Подраздел проекта");
            _cbParam2.Text = "ADSK_Штамп Раздел проекта";
            Grid.SetColumn(_cbParam2, 1);
            param2Grid.Children.Add(_cbParam2);

            _txtParamVal2 = new TextBox
            {
                Style = textBoxStyle,
                Text = "ОВ",
                Height = 24
            };
            Grid.SetColumn(_txtParamVal2, 3);
            param2Grid.Children.Add(_txtParamVal2);

            // 5. Нижняя панель с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 6);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка Создать (Премиальный алый градиент)
            _btnGenerate = new Button
            {
                Content = "Создать 3D схемы",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnGenerate.Click += BtnGenerate_Click;
            
            // Настройка градиентов для кнопки
            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnGenerate, normalGrad, hoverGrad, roundedButtonStyle);
            bottomGrid.Children.Add(_btnGenerate);

            // Кнопка Отмена (Темный скругленный)
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            
            SolidColorBrush cancelNormal = new SolidColorBrush(Color.FromRgb(58, 58, 66));
            SolidColorBrush cancelHover = new SolidColorBrush(Color.FromRgb(78, 78, 88));
            ApplyFlatButtonStyles(btnCancel, cancelNormal, cancelHover, roundedButtonStyle);
            
            Grid.SetColumn(btnCancel, 1);
            bottomGrid.Children.Add(btnCancel);

            StyleComboBoxDark(_cbFilterParam, controlBg, borderBrush);
            StyleComboBoxDark(_cbParam1, controlBg, borderBrush);
            StyleComboBoxDark(_cbParam2, controlBg, borderBrush);

            // Подписываемся на изменения галочек для обновления счетчика на кнопке
            foreach (var system in _allSystems)
            {
                system.PropertyChanged += System_PropertyChanged;
            }
            UpdateGenerateButtonText();
        }

        private void System_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemItem.IsChecked))
            {
                UpdateGenerateButtonText();
            }
        }

        private void UpdateGenerateButtonText()
        {
            int count = SelectedSystems.Count;
            _btnGenerate.Content = count > 0 ? $"Создать 3D схемы ({count})" : "Создать 3D схемы";
            _btnGenerate.IsEnabled = count > 0;
        }

        private void SetAllChecked(bool isChecked)
        {
            var currentList = _listBox.ItemsSource.Cast<SystemItem>();
            foreach (var item in currentList)
            {
                item.IsChecked = isChecked;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = _searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query) || query == "Поиск систем...")
            {
                _listBox.ItemsSource = _allSystems;
            }
            else
            {
                _listBox.ItemsSource = _allSystems
                    .Where(s => s.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
        }

        private void FilterParamSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = _txtFilterParamSearch.Text.Trim();
            if (string.IsNullOrEmpty(query) || query == "Поиск...")
            {
                _cbFilterParam.ItemsSource = _allFilterParams;
            }
            else
            {
                _cbFilterParam.ItemsSource = _allFilterParams
                    .Where(p => p.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
            if (_cbFilterParam.Items.Count > 0)
            {
                _cbFilterParam.SelectedIndex = 0;
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSystems.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы одну систему.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedParam = _cbFilterParam.SelectedItem as FilterParameterItem;
            if (selectedParam != null)
            {
                SelectedFilterParamId = selectedParam.Id;
            }
            else
            {
                SelectedFilterParamId = new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            }

            ViewPrefix = _txtPrefix.Text;
            ViewSuffix = _txtSuffix.Text;

            ViewParameters.Clear();
            if (_chkParam1.IsChecked == true && !string.IsNullOrWhiteSpace(_cbParam1.Text))
            {
                ViewParameters.Add(new ViewParameterConfig
                {
                    IsEnabled = true,
                    Name = _cbParam1.Text.Trim(),
                    Value = _txtParamVal1.Text
                });
            }
            if (_chkParam2.IsChecked == true && !string.IsNullOrWhiteSpace(_cbParam2.Text))
            {
                ViewParameters.Add(new ViewParameterConfig
                {
                    IsEnabled = true,
                    Name = _cbParam2.Text.Trim(),
                    Value = _txtParamVal2.Text
                });
            }

            this.DialogResult = true;
            this.Close();
        }

        private void StyleComboBoxDark(ComboBox cb, SolidColorBrush bg, SolidColorBrush borderBrush)
        {
            UiThemeHelper.StyleComboBoxDark(cb, bg, borderBrush);
        }
    }
}
