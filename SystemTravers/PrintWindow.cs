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
    public class SheetItem : INotifyPropertyChanged
    {
        private bool _isChecked = true;
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string PaperFormat { get; set; }
        public string Orientation { get; set; }
        public object RawSheetObject { get; set; }
        public bool IsView { get; set; }
        public string UniqueId { get; set; }

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

    public class PrintWindow : Window
    {
        private List<SheetItem> _allSheets;
        private Dictionary<string, List<string>> _sheetSetMap;
        private string _activeViewId;

        private ListBox _listBox;
        private TextBox _searchBox;
        private ComboBox _cbTypeFilter;
        private ComboBox _cbSheetSets;
        private CheckBox _chkCombine;
        private CheckBox _chkFixQuotes;
        private CheckBox _chkExportPdf;
        private CheckBox _chkExportDwg;
        private TextBox _txtNamingPattern;
        private TextBlock _txtNamingPreview;
        private TextBox _txtFolderPath;
        private Button _btnExport;

        private ComboBox _cbDwgSetups;
        private List<string> _dwgSetupNames;

        public string NamingPattern => _txtNamingPattern.Text;
        public string OutputFolderPath => _txtFolderPath.Text;
        public List<SheetItem> SelectedSheets => _allSheets.Where(s => s.IsChecked).ToList();
        public bool CombineToSinglePdf => _chkCombine.IsChecked == true;
        public bool FixQuotes => _chkFixQuotes?.IsChecked == true;
        public bool ExportPdf => _chkExportPdf.IsChecked == true;
        public bool ExportDwg => _chkExportDwg.IsChecked == true;
        public string SelectedDwgSetup => _cbDwgSetups?.SelectedItem?.ToString();
        public bool IsExportRequested { get; private set; } = false;

        private string _sampleProjNum = "2026-05";
        private string _sampleSection = "ОВ";

        public PrintWindow(List<SheetItem> sheets, string projNum, string defaultSection, Dictionary<string, List<string>> sheetSetMap, string activeViewId, List<string> dwgSetupNames)
        {
            _allSheets = sheets;
            _sheetSetMap = sheetSetMap;
            _activeViewId = activeViewId;
            _dwgSetupNames = dwgSetupNames;
            
            if (!string.IsNullOrEmpty(projNum)) _sampleProjNum = projNum;
            if (!string.IsNullOrEmpty(defaultSection)) _sampleSection = defaultSection;

            InitializeComponent();
            PopulateSheetSets();
            PopulateDwgSetups(dwgSetupNames);
            RefreshList();
            UpdateNamingPreview();
            ToggleDwgOptions();
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
            Title = "Экспорт чертежей - BimboClub Tools";
            Width = 560;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            MinHeight = 650;
            MinWidth = 500;
            UiThemeHelper.ApplyDarkTheme(this);

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B (Scarlet)
            SolidColorBrush textMuted = new SolidColorBrush(Color.FromRgb(160, 160, 170));

            // Общие стили
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

            Style checkBoxStyle = new Style(typeof(CheckBox));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brushes.White));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center));

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главная сетка
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });    // Поиск
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список листов
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });    // Групповой выбор
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(315) });   // Настройки
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });    // Кнопки действий
            Content = mainGrid;

            // 1. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(18, 18, 20), Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "ПАКЕТНЫЙ ЭКСПОРТ ЧЕРТЕЖЕЙ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Поиск
            Grid searchGrid = new Grid { Margin = new Thickness(15, 5, 15, 5) };
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            Grid.SetRow(searchGrid, 1);
            mainGrid.Children.Add(searchGrid);

            _searchBox = new TextBox
            {
                Style = textBoxStyle,
                Text = "Поиск по номеру или имени...",
                Foreground = Brushes.Gray
            };
            _searchBox.GotFocus += (s, e) =>
            {
                _searchBox.BorderBrush = activeBorderBrush;
                if (_searchBox.Text == "Поиск по номеру или имени...")
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
                    _searchBox.Text = "Поиск по номеру или имени...";
                    _searchBox.Foreground = Brushes.Gray;
                }
            };
            _searchBox.TextChanged += (s, e) => RefreshList();
            Grid.SetColumn(_searchBox, 0);
            searchGrid.Children.Add(_searchBox);

            _cbTypeFilter = new ComboBox
            {
                Style = comboBoxStyle,
                Height = 26,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            UiThemeHelper.StyleComboBoxDark(_cbTypeFilter, controlBg, borderBrush);
            _cbTypeFilter.Items.Add("Все");
            _cbTypeFilter.Items.Add("Только листы");
            _cbTypeFilter.Items.Add("Только виды");
            _cbTypeFilter.SelectedIndex = 0;
            _cbTypeFilter.SelectionChanged += (s, e) => RefreshList();
            Grid.SetColumn(_cbTypeFilter, 1);
            searchGrid.Children.Add(_cbTypeFilter);

            // 3. Список листов
            Border listBorder = new Border
            {
                Margin = new Thickness(15, 5, 15, 5),
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetRow(listBorder, 2);
            mainGrid.Children.Add(listBorder);

            _listBox = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                SelectionMode = SelectionMode.Single
            };
            listBorder.Child = _listBox;

            // 4. Панель быстрого выбора
            StackPanel selectStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(15, 2, 15, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(selectStack, 3);
            mainGrid.Children.Add(selectStack);

            Button btnSelectAll = new Button { Content = "Выбрать все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand, Width = 120, Height = 28, FontSize = 11 };
            ApplyFlatButtonStyles(btnSelectAll, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnSelectAll.Click += (s, e) => { SetAllSelection(true); };
            selectStack.Children.Add(btnSelectAll);

            Button btnDeselectAll = new Button { Content = "Снять выделение", Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Width = 150, Height = 28, FontSize = 11 };
            ApplyFlatButtonStyles(btnDeselectAll, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnDeselectAll.Click += (s, e) => { SetAllSelection(false); };
            selectStack.Children.Add(btnDeselectAll);

            // 5. Настройки
            Border settingsBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(15, 5, 15, 10)
            };
            Grid.SetRow(settingsBorder, 4);
            mainGrid.Children.Add(settingsBorder);

            Grid settingsGrid = new Grid();
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) }); // Заголовок
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Выбор набора
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Форматы экспорта (PDF, DWG)
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Настройка экспорта DWG
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) }); // Объединение PDF
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) }); // Исправление кавычек
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Шаблон имени
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) }); // Превью имени
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Папка
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) }); // Теги описание
            settingsBorder.Child = settingsGrid;

            TextBlock lblSettingsTitle = new TextBlock
            {
                Text = "НАСТРОЙКИ НАБОРА И ЭКСПОРТА:",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            settingsGrid.Children.Add(lblSettingsTitle);

            // 5.1 Выбор наборов
            Grid setGrid = new Grid();
            Grid.SetRow(setGrid, 1);
            settingsGrid.Children.Add(setGrid);
            setGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            setGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblSets = new TextBlock { Text = "Выбрать набор листов:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblSets, 0);
            setGrid.Children.Add(lblSets);

            _cbSheetSets = new ComboBox { Style = comboBoxStyle, Height = 24 };
            UiThemeHelper.StyleComboBoxDark(_cbSheetSets, controlBg, borderBrush);
            Grid.SetColumn(_cbSheetSets, 1);
            setGrid.Children.Add(_cbSheetSets);

            // 5.1.5 Форматы экспорта (PDF, DWG)
            Grid formatGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(formatGrid, 2);
            settingsGrid.Children.Add(formatGrid);
            formatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            formatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            formatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            TextBlock lblFormats = new TextBlock { Text = "Форматы экспорта:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblFormats, 0);
            formatGrid.Children.Add(lblFormats);

            _chkExportPdf = new CheckBox { Style = checkBoxStyle, Content = "PDF", IsChecked = true, VerticalContentAlignment = VerticalAlignment.Center };
            _chkExportPdf.Checked += (s, e) => { TogglePdfOptions(); UpdateNamingPreview(); };
            _chkExportPdf.Unchecked += (s, e) => { TogglePdfOptions(); UpdateNamingPreview(); };
            Grid.SetColumn(_chkExportPdf, 1);
            formatGrid.Children.Add(_chkExportPdf);

            _chkExportDwg = new CheckBox { Style = checkBoxStyle, Content = "DWG", IsChecked = false, VerticalContentAlignment = VerticalAlignment.Center };
            _chkExportDwg.Checked += (s, e) => { ToggleDwgOptions(); UpdateNamingPreview(); };
            _chkExportDwg.Unchecked += (s, e) => { ToggleDwgOptions(); UpdateNamingPreview(); };
            Grid.SetColumn(_chkExportDwg, 2);
            formatGrid.Children.Add(_chkExportDwg);

            // 5.1.8 Настройка экспорта DWG (Ряд 3)
            Grid dwgGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(dwgGrid, 3);
            settingsGrid.Children.Add(dwgGrid);
            dwgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            dwgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblDwgSetup = new TextBlock { Text = "Настройка экспорта DWG:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblDwgSetup, 0);
            dwgGrid.Children.Add(lblDwgSetup);

            _cbDwgSetups = new ComboBox { Style = comboBoxStyle, Height = 24 };
            UiThemeHelper.StyleComboBoxDark(_cbDwgSetups, controlBg, borderBrush);
            Grid.SetColumn(_cbDwgSetups, 1);
            dwgGrid.Children.Add(_cbDwgSetups);

            // 5.2 Объединение в один PDF (Ряд 4)
            _chkCombine = new CheckBox
            {
                Style = checkBoxStyle,
                Content = "Объединить все выбранные листы в один общий PDF файл",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _chkCombine.Checked += (s, e) => { UpdateNamingPreview(); };
            _chkCombine.Unchecked += (s, e) => { UpdateNamingPreview(); };
            Grid.SetRow(_chkCombine, 4);
            settingsGrid.Children.Add(_chkCombine);

            // 5.2.5 Исправление кавычек ГОСТ (Ряд 5)
            _chkFixQuotes = new CheckBox
            {
                Style = checkBoxStyle,
                Content = "Исправлять кавычки-ёлочки « » (ГОСТ без дробей 1/4 и 1/2)",
                FontWeight = FontWeights.SemiBold,
                IsChecked = true,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetRow(_chkFixQuotes, 5);
            settingsGrid.Children.Add(_chkFixQuotes);

            // 5.3 Шаблон имени (Ряд 6)
            Grid patternGrid = new Grid();
            Grid.SetRow(patternGrid, 6);
            settingsGrid.Children.Add(patternGrid);
            patternGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            patternGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblPattern = new TextBlock { Text = "Шаблон имени файла:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblPattern, 0);
            patternGrid.Children.Add(lblPattern);

            _txtNamingPattern = new TextBox
            {
                Style = textBoxStyle,
                Text = "{ProjectNumber}_{Section}_{SheetNumber}_{SheetName}",
                Height = 24
            };
            _txtNamingPattern.TextChanged += (s, e) => UpdateNamingPreview();
            Grid.SetColumn(_txtNamingPattern, 1);
            patternGrid.Children.Add(_txtNamingPattern);

            // 5.4 Превью (Ряд 7)
            Border previewBorder = new Border
            {
                Background = controlBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 2, 0, 2)
            };
            Grid.SetRow(previewBorder, 7);
            settingsGrid.Children.Add(previewBorder);

            _txtNamingPreview = new TextBlock
            {
                Text = "Пример имени: ",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            previewBorder.Child = _txtNamingPreview;

            // 5.5 Папка сохранения (Ряд 8)
            Grid folderGrid = new Grid();
            Grid.SetRow(folderGrid, 8);
            settingsGrid.Children.Add(folderGrid);
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });

            TextBlock lblFolder = new TextBlock { Text = "Папка сохранения:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblFolder, 0);
            folderGrid.Children.Add(lblFolder);

            _txtFolderPath = new TextBox
            {
                Style = textBoxStyle,
                Height = 24,
                IsReadOnly = true,
                Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(_txtFolderPath, 1);
            folderGrid.Children.Add(_txtFolderPath);

            Button btnBrowse = new Button { Content = "Обзор...", Foreground = Brushes.White, BorderThickness = new Thickness(0), Height = 24, Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnBrowse, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnBrowse.Click += BtnBrowse_Click;
            Grid.SetColumn(btnBrowse, 2);
            folderGrid.Children.Add(btnBrowse);

            // 5.6 Теги описание (Ряд 9)
            TextBlock lblTagsDesc = new TextBlock
            {
                Text = "Доступные теги: {ProjectNumber} (Шифр), {Section} (Раздел), {SheetNumber} (Номер), {SheetName} (Имя листов)",
                Foreground = textMuted,
                FontSize = 9.5,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lblTagsDesc, 9);
            settingsGrid.Children.Add(lblTagsDesc);

            // 6. Подвал
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 5);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка экспорта
            _btnExport = new Button
            {
                Content = "Экспорт в PDF",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnExport.Click += BtnExport_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnExport, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnExport, 1);
            bottomGrid.Children.Add(_btnExport);

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

        private void PopulateSheetSets()
        {
            _cbSheetSets.Items.Clear();
            _cbSheetSets.Items.Add("<Выбрать сохраненный набор...>");
            _cbSheetSets.Items.Add("<Все элементы>");
            if (!string.IsNullOrEmpty(_activeViewId))
            {
                _cbSheetSets.Items.Add("<Активный вид/лист>");
            }
            if (_sheetSetMap != null)
            {
                foreach (var name in _sheetSetMap.Keys)
                {
                    _cbSheetSets.Items.Add(name);
                }
            }
            _cbSheetSets.SelectedIndex = 0;
            _cbSheetSets.SelectionChanged += CbSheetSets_SelectionChanged;
        }

        private void CbSheetSets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cbSheetSets.SelectedIndex <= 0) return;

            string selected = _cbSheetSets.SelectedItem.ToString();
            if (selected == "<Все элементы>")
            {
                foreach (var item in _allSheets) item.IsChecked = true;
            }
            else if (selected == "<Активный вид/лист>")
            {
                foreach (var item in _allSheets)
                {
                    item.IsChecked = (item.UniqueId == _activeViewId);
                }
            }
            else if (_sheetSetMap != null && _sheetSetMap.ContainsKey(selected))
            {
                List<string> ids = _sheetSetMap[selected];
                foreach (var item in _allSheets)
                {
                    item.IsChecked = ids.Contains(item.UniqueId);
                }
            }
            RefreshList();
        }

        private void SetAllSelection(bool isSelected)
        {
            foreach (var item in _allSheets)
            {
                item.IsChecked = isSelected;
            }
            RefreshList();
        }

        private void RefreshList()
        {
            if (_listBox == null) return;

            _listBox.Items.Clear();

            string filter = _searchBox.Text;
            bool isFilterActive = !string.IsNullOrWhiteSpace(filter) && filter != "Поиск по номеру или имени..." && filter != "Поиск по номеру или имени листа...";

            var filtered = _allSheets;

            if (_cbTypeFilter != null && _cbTypeFilter.SelectedIndex > 0)
            {
                if (_cbTypeFilter.SelectedIndex == 1) // Только листы
                {
                    filtered = filtered.Where(s => !s.IsView).ToList();
                }
                else if (_cbTypeFilter.SelectedIndex == 2) // Только виды
                {
                    filtered = filtered.Where(s => s.IsView).ToList();
                }
            }

            if (isFilterActive)
            {
                filtered = filtered.Where(s => 
                    (s.SheetNumber != null && s.SheetNumber.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (s.SheetName != null && s.SheetName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            foreach (var item in filtered)
            {
                Grid rowGrid = new Grid { Margin = new Thickness(5, 4, 5, 4) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // Чекбокс
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Номер
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Имя
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) }); // Формат

                CheckBox chk = new CheckBox
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = item.IsChecked
                };
                chk.Checked += (s, e) => { item.IsChecked = true; };
                chk.Unchecked += (s, e) => { item.IsChecked = false; };
                Grid.SetColumn(chk, 0);
                rowGrid.Children.Add(chk);

                TextBlock txtNum = new TextBlock
                {
                    Text = item.SheetNumber,
                    Foreground = item.IsView ? new SolidColorBrush(Color.FromRgb(46, 179, 255)) : Brushes.White,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txtNum, 1);
                rowGrid.Children.Add(txtNum);

                TextBlock txtName = new TextBlock
                {
                    Text = item.SheetName,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 225)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(txtName, 2);
                rowGrid.Children.Add(txtName);

                Border formatBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(48, 48, 56)),
                    BorderBrush = item.IsView ? Brushes.Gray : (item.PaperFormat == "Не определен" ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(255, 46, 91))),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                TextBlock txtFormat = new TextBlock
                {
                    Text = item.IsView ? "Вид" : $"{item.PaperFormat} ({item.Orientation.Substring(0, 3)}.)",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                formatBorder.Child = txtFormat;
                Grid.SetColumn(formatBorder, 3);
                rowGrid.Children.Add(formatBorder);

                _listBox.Items.Add(rowGrid);
            }
        }

        private void UpdateNamingPreview()
        {
            if (_txtNamingPreview == null || _txtNamingPattern == null) return;

            string pattern = _txtNamingPattern.Text;
            string preview;
            if (_chkCombine != null && _chkCombine.IsChecked == true && _chkExportPdf.IsChecked == true)
            {
                preview = pattern
                    .Replace("{ProjectNumber}", _sampleProjNum)
                    .Replace("{Section}", _sampleSection)
                    .Replace("{SheetNumber}", "Комплект")
                    .Replace("{SheetName}", "Чертежей");
            }
            else
            {
                preview = pattern
                    .Replace("{ProjectNumber}", _sampleProjNum)
                    .Replace("{Section}", _sampleSection)
                    .Replace("{SheetNumber}", "01")
                    .Replace("{SheetName}", "План_1_этажа");
            }

            string ext = "";
            if (_chkExportPdf?.IsChecked == true) ext += ".pdf";
            if (_chkExportDwg?.IsChecked == true) ext += ".dwg";
            if (string.IsNullOrEmpty(ext)) ext = ".pdf";

            _txtNamingPreview.Text = $"Пример имени: {preview}{ext}";
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для сохранения экспортированных чертежей";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtFolderPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedSheets;
            if (selected.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один лист для экспорта.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_chkExportPdf.IsChecked == false && _chkExportDwg.IsChecked == false)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один формат для экспорта (PDF, DWG).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtFolderPath.Text) || !System.IO.Directory.Exists(_txtFolderPath.Text))
            {
                MessageBox.Show("Пожалуйста, выберите существующую папку для сохранения файлов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsExportRequested = true;
            DialogResult = true;
            Close();
        }

        private void TogglePdfOptions()
        {
            if (_chkCombine != null)
            {
                _chkCombine.IsEnabled = _chkExportPdf.IsChecked == true;
            }
        }

        private void PopulateDwgSetups(List<string> names)
        {
            if (_cbDwgSetups == null) return;
            _cbDwgSetups.Items.Clear();
            _cbDwgSetups.Items.Add("<По умолчанию>");
            if (names != null)
            {
                foreach (var name in names)
                {
                    _cbDwgSetups.Items.Add(name);
                }
            }
            _cbDwgSetups.SelectedIndex = 0;
        }

        private void ToggleDwgOptions()
        {
            if (_cbDwgSetups != null)
            {
                _cbDwgSetups.IsEnabled = _chkExportDwg.IsChecked == true;
            }
        }
    }
}
