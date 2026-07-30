using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class ScheduleExportItem : INotifyPropertyChanged
    {
        private bool _isChecked = false;
        public string ScheduleName { get; set; }
        public object RawSchedule { get; set; }

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

    public class InputPromptWindow : Window
    {
        private TextBox _txtInput;
        public string InputText => _txtInput.Text.Trim();

        public InputPromptWindow(string title, string prompt, string defaultVal = "")
        {
            Title = title;
            Width = 350;
            Height = 155;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
            ResizeMode = ResizeMode.NoResize;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ShowInTaskbar = false;

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            Content = grid;

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };
            TextBlock lbl = new TextBlock { Text = prompt, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            _txtInput = new TextBox
            {
                Text = defaultVal,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)),
                Padding = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(lbl);
            panel.Children.Add(_txtInput);
            Grid.SetRow(panel, 0);
            grid.Children.Add(panel);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 15, 10) };
            Button btnOk = new Button { Content = "OK", Width = 80, Height = 28, Margin = new Thickness(0, 0, 10, 0), Foreground = Brushes.White };
            Button btnCancel = new Button { Content = "Отмена", Width = 80, Height = 28, Foreground = Brushes.White };

            var style = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            border.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            btnOk.Style = style;
            btnOk.Background = new SolidColorBrush(Color.FromRgb(179, 14, 45));
            btnOk.Click += (s, e) => { if (!string.IsNullOrEmpty(InputText)) { DialogResult = true; Close(); } };

            btnCancel.Style = style;
            btnCancel.Background = new SolidColorBrush(Color.FromRgb(55, 55, 65));
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            buttons.Children.Add(btnOk);
            buttons.Children.Add(btnCancel);
            Grid.SetRow(buttons, 1);
            grid.Children.Add(buttons);
        }
    }

    public class ExcelExportWindow : Window
    {
        private List<ScheduleExportItem> _allSchedules;
        private ExcelExportSettings _settings;
        private bool _isUpdatingUi = false;

        private ListBox _lbSchedules;
        private TextBox _txtSearch;
        private TextBlock _lblSelectedCount;

        private ComboBox _cbSets;
        private ComboBox _cbConfigs;

        private TextBox _txtFolder;
        private CheckBox _chkSeparate;
        private TextBox _txtPrefix;
        private TextBox _txtFileName;
        private CheckBox _chkHeader;
        private CheckBox _chkFormatting;

        private Button _btnExport;
        private Button _btnExportCurrent;

        public List<object> SelectedSchedules => _allSchedules.Where(s => s.IsChecked).Select(s => s.RawSchedule).ToList();
        public bool ExportCurrentOnly { get; private set; } = false;

        public ExcelExportWindow(List<ScheduleExportItem> schedules)
        {
            _allSchedules = schedules;
            _settings = ExcelExportSettings.Load();

            InitializeComponent();
            LoadSets();
            LoadConfigs();

            // Load last settings
            _txtFolder.Text = _settings.ExportFolder;
            _chkSeparate.IsChecked = _settings.EachScheduleInSeparateFile;
            _txtPrefix.Text = _settings.FilePrefix;
            _txtFileName.Text = _settings.FileName;
            _chkHeader.IsChecked = _settings.ExportHeader;
            _chkFormatting.IsChecked = _settings.PreserveFormatting;

            RefreshSchedulesFilter();
            UpdateSelectedCount();
            ToggleFileNameField();
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
            Title = "Экспорт в Excel - BimboClub Tools";
            Width = 500;
            Height = 780;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;

            // Colors & brushes
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorder = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B
            LinearGradientBrush headerGrad = new LinearGradientBrush(Color.FromRgb(18, 18, 20), Color.FromRgb(179, 14, 45), 0.0);
            LinearGradientBrush redBtnGrad = new LinearGradientBrush(Color.FromRgb(179, 14, 45), Color.FromRgb(135, 10, 34), 90.0);
            LinearGradientBrush redBtnHover = new LinearGradientBrush(Color.FromRgb(210, 20, 58), Color.FromRgb(179, 14, 45), 90.0);

            // Styles
            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, controlBg));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(5)));
            textBoxStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style roundedBtnStyle = GetRoundedButtonStyle(3);

            // Main Grid
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });    // 0: Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Content Scroll/Grid
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // 2: Footer
            Content = mainGrid;

            // 0. Header
            Border headerBorder = new Border { Background = headerGrad, Padding = new Thickness(15, 10, 15, 10) };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "ЭКСПОРТ В EXCEL",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 1. Content Panel
            StackPanel contentPanel = new StackPanel { Margin = new Thickness(15, 10, 15, 10) };
            Grid.SetRow(contentPanel, 1);
            mainGrid.Children.Add(contentPanel);

            // Group 1: Спецификации
            GroupBox gbSchedules = new GroupBox
            {
                Header = "Спецификации",
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            contentPanel.Children.Add(gbSchedules);

            StackPanel schedStack = new StackPanel();
            gbSchedules.Content = schedStack;

            // Set selector row
            Grid setGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            setGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            setGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            setGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            schedStack.Children.Add(setGrid);

            TextBlock lblSet = new TextBlock { Text = "Набор:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblSet, 0);
            setGrid.Children.Add(lblSet);

            _cbSets = new ComboBox { Background = controlBg, Foreground = Brushes.White, BorderBrush = borderBrush, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            _cbSets.SelectionChanged += CbSets_SelectionChanged;
            Grid.SetColumn(_cbSets, 1);
            setGrid.Children.Add(_cbSets);

            // Mini Set Action Buttons (+ Save Edit Delete)
            StackPanel setActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(setActions, 2);
            setGrid.Children.Add(setActions);

            Button btnAddSet = CreateMiniButton("+", "Создать новый набор");
            btnAddSet.Click += BtnAddSet_Click;
            setActions.Children.Add(btnAddSet);

            Button btnSaveSet = CreateMiniButton("💾", "Сохранить текущий выбор в набор");
            btnSaveSet.Click += BtnSaveSet_Click;
            setActions.Children.Add(btnSaveSet);

            Button btnEditSet = CreateMiniButton("✏️", "Переименовать набор");
            btnEditSet.Click += BtnEditSet_Click;
            setActions.Children.Add(btnEditSet);

            Button btnDeleteSet = CreateMiniButton("🗑️", "Удалить набор");
            btnDeleteSet.Click += BtnDeleteSet_Click;
            setActions.Children.Add(btnDeleteSet);

            // Search Box
            Grid searchGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            schedStack.Children.Add(searchGrid);

            _txtSearch = new TextBox
            {
                Style = textBoxStyle,
                Height = 26,
                Margin = new Thickness(0),
                Padding = new Thickness(5, 0, 5, 0)
            };
            // Add a visual tip: Search / Поиск
            _txtSearch.TextChanged += (s, e) => RefreshSchedulesFilter();
            Grid.SetColumn(_txtSearch, 0);
            searchGrid.Children.Add(_txtSearch);

            Button btnClearSearch = new Button { Content = "✕", Width = 26, Height = 26, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ApplyFlatButtonStyles(btnClearSearch, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), roundedBtnStyle);
            btnClearSearch.Click += (s, e) => _txtSearch.Text = "";
            Grid.SetColumn(btnClearSearch, 1);
            searchGrid.Children.Add(btnClearSearch);

            // Schedules Checklist ListBox
            _lbSchedules = new ListBox
            {
                Background = controlBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Height = 160,
                Margin = new Thickness(0, 0, 0, 8)
            };

            DataTemplate itemTemplate = new DataTemplate();
            FrameworkElementFactory itemGrid = new FrameworkElementFactory(typeof(Grid));
            itemGrid.SetValue(Grid.HeightProperty, 26.0);
            FrameworkElementFactory chk = new FrameworkElementFactory(typeof(CheckBox));
            chk.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chk.SetValue(CheckBox.ForegroundProperty, Brushes.White);
            chk.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Mode = BindingMode.TwoWay });
            chk.SetBinding(CheckBox.ContentProperty, new Binding("ScheduleName"));
            chk.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler((s, ev) => {
                UpdateSelectedCount();
                _settings.SelectedSet = "";
                _cbSets.SelectedIndex = -1;
            }));
            itemGrid.AppendChild(chk);
            itemTemplate.VisualTree = itemGrid;
            _lbSchedules.ItemTemplate = itemTemplate;
            schedStack.Children.Add(_lbSchedules);

            // Selected count & visibility selection buttons
            Grid selectionButtonsGrid = new Grid();
            selectionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            selectionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selectionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            schedStack.Children.Add(selectionButtonsGrid);

            _lblSelectedCount = new TextBlock { Text = "Выбрано: 0", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_lblSelectedCount, 0);
            selectionButtonsGrid.Children.Add(_lblSelectedCount);

            StackPanel rightSelectStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(rightSelectStack, 2);
            selectionButtonsGrid.Children.Add(rightSelectStack);

            Button btnSelectAllVisible = new Button { Content = "Выбрать все видимые", Width = 150, Height = 24, Margin = new Thickness(0, 0, 10, 0), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ApplyFlatButtonStyles(btnSelectAllVisible, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), roundedBtnStyle);
            btnSelectAllVisible.Click += (s, e) => SetVisibleSelection(true);
            rightSelectStack.Children.Add(btnSelectAllVisible);

            Button btnDeselectAllVisible = new Button { Content = "Снять выбор со всех видимых", Width = 190, Height = 24, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ApplyFlatButtonStyles(btnDeselectAllVisible, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), roundedBtnStyle);
            btnDeselectAllVisible.Click += (s, e) => SetVisibleSelection(false);
            rightSelectStack.Children.Add(btnDeselectAllVisible);

            // Group 2: Опции экспорта
            GroupBox gbOptions = new GroupBox
            {
                Header = "Опции экспорта",
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Padding = new Thickness(10)
            };
            contentPanel.Children.Add(gbOptions);

            StackPanel optStack = new StackPanel();
            gbOptions.Content = optStack;

            // Config Selector row
            Grid configGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            optStack.Children.Add(configGrid);

            TextBlock lblConfig = new TextBlock { Text = "Конфигурация:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblConfig, 0);
            configGrid.Children.Add(lblConfig);

            _cbConfigs = new ComboBox { Background = controlBg, Foreground = Brushes.White, BorderBrush = borderBrush, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            _cbConfigs.SelectionChanged += CbConfigs_SelectionChanged;
            Grid.SetColumn(_cbConfigs, 1);
            configGrid.Children.Add(_cbConfigs);

            StackPanel configActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(configActions, 2);
            configGrid.Children.Add(configActions);

            Button btnAddConfig = CreateMiniButton("+", "Создать конфигурацию");
            btnAddConfig.Click += BtnAddConfig_Click;
            configActions.Children.Add(btnAddConfig);

            Button btnEditConfig = CreateMiniButton("✏️", "Переименовать конфигурацию");
            btnEditConfig.Click += BtnEditConfig_Click;
            configActions.Children.Add(btnEditConfig);

            Button btnDeleteConfig = CreateMiniButton("🗑️", "Удалить конфигурацию");
            btnDeleteConfig.Click += BtnDeleteConfig_Click;
            configActions.Children.Add(btnDeleteConfig);

            // Folder selector row
            Grid folderGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            optStack.Children.Add(folderGrid);

            TextBlock lblFolder = new TextBlock { Text = "Папка экспорта:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblFolder, 0);
            folderGrid.Children.Add(lblFolder);

            _txtFolder = new TextBox { Style = textBoxStyle, Height = 26 };
            Grid.SetColumn(_txtFolder, 1);
            folderGrid.Children.Add(_txtFolder);

            Button btnBrowse = new Button { Content = "Выбрать", Width = 70, Height = 26, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ApplyFlatButtonStyles(btnBrowse, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), roundedBtnStyle);
            btnBrowse.Click += BtnBrowse_Click;
            Grid.SetColumn(btnBrowse, 2);
            folderGrid.Children.Add(btnBrowse);

            // Checkbox: Separate files
            _chkSeparate = new CheckBox
            {
                Content = "Каждую спецификацию в отдельный файл",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _chkSeparate.Checked += (s, e) => ToggleFileNameField();
            _chkSeparate.Unchecked += (s, e) => ToggleFileNameField();
            optStack.Children.Add(_chkSeparate);

            // TextBox: File prefix
            Grid prefixGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optStack.Children.Add(prefixGrid);

            TextBlock lblPrefix = new TextBlock { Text = "Префикс имени файла:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblPrefix, 0);
            prefixGrid.Children.Add(lblPrefix);

            _txtPrefix = new TextBox { Style = textBoxStyle, Height = 26 };
            Grid.SetColumn(_txtPrefix, 1);
            prefixGrid.Children.Add(_txtPrefix);

            // TextBox: File name (single workbook)
            Grid filenameGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            filenameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            filenameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filenameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            optStack.Children.Add(filenameGrid);

            TextBlock lblFileName = new TextBlock { Text = "Имя файла:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblFileName, 0);
            filenameGrid.Children.Add(lblFileName);

            _txtFileName = new TextBox { Style = textBoxStyle, Height = 26 };
            Grid.SetColumn(_txtFileName, 1);
            filenameGrid.Children.Add(_txtFileName);

            TextBlock lblExt = new TextBlock { Text = ".xls", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            Grid.SetColumn(lblExt, 2);
            filenameGrid.Children.Add(lblExt);

            // Format checkboxes
            _chkHeader = new CheckBox
            {
                Content = "Экспортировать заголовок",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            optStack.Children.Add(_chkHeader);

            _chkFormatting = new CheckBox
            {
                Content = "Сохранить форматирование",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            optStack.Children.Add(_chkFormatting);

            // 2. Footer
            Border footerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(18, 18, 20)), Padding = new Thickness(15, 10, 15, 10) };
            Grid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);

            Grid footerGrid = new Grid();
            footerBorder.Child = footerGrid;
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            _btnExportCurrent = new Button { Content = "Экспорт текущей", Height = 32, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ApplyFlatButtonStyles(_btnExportCurrent, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), GetRoundedButtonStyle(4));
            _btnExportCurrent.Click += BtnExportCurrent_Click;
            Grid.SetColumn(_btnExportCurrent, 1);
            footerGrid.Children.Add(_btnExportCurrent);

            _btnExport = new Button { Content = "Экспорт", Height = 32, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold };
            ApplyButtonStyles(_btnExport, redBtnGrad, redBtnHover, GetRoundedButtonStyle(4));
            _btnExport.Click += BtnExportAll_Click;
            Grid.SetColumn(_btnExport, 3);
            footerGrid.Children.Add(_btnExport);

            Button btnClose = new Button { Content = "Закрыть", Height = 32, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ApplyFlatButtonStyles(btnClose, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), GetRoundedButtonStyle(4));
            btnClose.Click += (s, e) => { DialogResult = false; Close(); };
            Grid.SetColumn(btnClose, 5);
            footerGrid.Children.Add(btnClose);

            // ComboBox custom item styling
            UiThemeHelper.StyleComboBoxDark(_cbSets, controlBg, borderBrush);
            UiThemeHelper.StyleComboBoxDark(_cbConfigs, controlBg, borderBrush);
        }

        private Button CreateMiniButton(string content, string tooltip)
        {
            Button btn = new Button
            {
                Content = content,
                Width = 22,
                Height = 22,
                Margin = new Thickness(3, 0, 0, 0),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                ToolTip = tooltip
            };
            ApplyFlatButtonStyles(btn, new SolidColorBrush(Color.FromRgb(55, 55, 65)), new SolidColorBrush(Color.FromRgb(75, 75, 85)), GetRoundedButtonStyle(2));
            return btn;
        }

        private void ToggleFileNameField()
        {
            if (_chkSeparate != null && _txtFileName != null)
            {
                bool separate = _chkSeparate.IsChecked == true;
                _txtFileName.IsEnabled = !separate;
            }
        }

        private void UpdateSelectedCount()
        {
            if (_lblSelectedCount != null)
            {
                int count = SelectedSchedules.Count;
                _lblSelectedCount.Text = $"Выбрано: {count}";
            }
        }

        private void RefreshSchedulesFilter()
        {
            string filterText = _txtSearch.Text.Trim().ToLower();
            var filtered = _allSchedules
                .Where(s => string.IsNullOrEmpty(filterText) || s.ScheduleName.ToLower().Contains(filterText))
                .OrderBy(s => s.ScheduleName)
                .ToList();

            _lbSchedules.ItemsSource = filtered;
        }

        private void SetVisibleSelection(bool isChecked)
        {
            var visibleItems = _lbSchedules.ItemsSource as List<ScheduleExportItem>;
            if (visibleItems != null)
            {
                foreach (var item in visibleItems)
                {
                    item.IsChecked = isChecked;
                }
            }
            UpdateSelectedCount();
        }

        // ----------------------------------------------------
        // Sets (Наборы) Management
        // ----------------------------------------------------
        private void LoadSets()
        {
            _cbSets.ItemsSource = _settings.Sets.Select(s => s.Name).ToList();
            if (!string.IsNullOrEmpty(_settings.SelectedSet))
            {
                _cbSets.SelectedItem = _settings.SelectedSet;
            }
        }

        private void CbSets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _cbSets.SelectedIndex == -1) return;
            _isUpdatingUi = true;

            string selectedSetName = _cbSets.SelectedItem as string;
            var set = _settings.Sets.FirstOrDefault(s => s.Name == selectedSetName);
            if (set != null)
            {
                foreach (var item in _allSchedules)
                {
                    item.IsChecked = set.SelectedScheduleNames.Contains(item.ScheduleName);
                }
                _settings.SelectedSet = selectedSetName;
                _settings.Save();
            }

            _isUpdatingUi = false;
            UpdateSelectedCount();
        }

        private void BtnAddSet_Click(object sender, RoutedEventArgs e)
        {
            var prompt = new InputPromptWindow("Создать набор", "Введите имя нового набора спецификаций:");
            prompt.Owner = this;
            if (prompt.ShowDialog() == true)
            {
                string name = prompt.InputText;
                if (_settings.Sets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Набор с таким именем уже существует.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newSet = new ScheduleSet
                {
                    Name = name,
                    SelectedScheduleNames = _allSchedules.Where(s => s.IsChecked).Select(s => s.ScheduleName).ToList()
                };
                _settings.Sets.Add(newSet);
                _settings.SelectedSet = name;
                _settings.Save();

                _isUpdatingUi = true;
                LoadSets();
                _isUpdatingUi = false;
            }
        }

        private void BtnSaveSet_Click(object sender, RoutedEventArgs e)
        {
            string selectedSetName = _cbSets.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedSetName))
            {
                MessageBox.Show("Пожалуйста, выберите набор для сохранения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var set = _settings.Sets.FirstOrDefault(s => s.Name == selectedSetName);
            if (set != null)
            {
                set.SelectedScheduleNames = _allSchedules.Where(s => s.IsChecked).Select(s => s.ScheduleName).ToList();
                _settings.Save();
                MessageBox.Show($"Набор '{selectedSetName}' успешно сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEditSet_Click(object sender, RoutedEventArgs e)
        {
            string selectedSetName = _cbSets.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedSetName)) return;

            var prompt = new InputPromptWindow("Переименовать набор", "Введите новое имя набора:", selectedSetName);
            prompt.Owner = this;
            if (prompt.ShowDialog() == true)
            {
                string newName = prompt.InputText;
                if (_settings.Sets.Any(s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Набор с таким именем уже существует.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var set = _settings.Sets.FirstOrDefault(s => s.Name == selectedSetName);
                if (set != null)
                {
                    set.Name = newName;
                    _settings.SelectedSet = newName;
                    _settings.Save();

                    _isUpdatingUi = true;
                    LoadSets();
                    _isUpdatingUi = false;
                }
            }
        }

        private void BtnDeleteSet_Click(object sender, RoutedEventArgs e)
        {
            string selectedSetName = _cbSets.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedSetName)) return;

            var res = MessageBox.Show($"Вы уверены, что хотите удалить набор '{selectedSetName}'?", "Удаление набора", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                var set = _settings.Sets.FirstOrDefault(s => s.Name == selectedSetName);
                if (set != null)
                {
                    _settings.Sets.Remove(set);
                    _settings.SelectedSet = "";
                    _settings.Save();

                    _isUpdatingUi = true;
                    LoadSets();
                    _cbSets.SelectedIndex = -1;
                    _isUpdatingUi = false;
                }
            }
        }

        // ----------------------------------------------------
        // Configurations (Конфигурации) Management
        // ----------------------------------------------------
        private void LoadConfigs()
        {
            _cbConfigs.ItemsSource = _settings.Configs.Select(c => c.Name).ToList();
            if (!string.IsNullOrEmpty(_settings.SelectedConfig))
            {
                _cbConfigs.SelectedItem = _settings.SelectedConfig;
            }
        }

        private void CbConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _cbConfigs.SelectedIndex == -1) return;
            _isUpdatingUi = true;

            string selectedConfigName = _cbConfigs.SelectedItem as string;
            var config = _settings.Configs.FirstOrDefault(c => c.Name == selectedConfigName);
            if (config != null)
            {
                _txtFolder.Text = config.ExportFolder;
                _chkSeparate.IsChecked = config.EachScheduleInSeparateFile;
                _txtPrefix.Text = config.FilePrefix;
                _txtFileName.Text = config.FileName;
                _chkHeader.IsChecked = config.ExportHeader;
                _chkFormatting.IsChecked = config.PreserveFormatting;

                _settings.SelectedConfig = selectedConfigName;
                _settings.Save();
                ToggleFileNameField();
            }

            _isUpdatingUi = false;
        }

        private void BtnAddConfig_Click(object sender, RoutedEventArgs e)
        {
            var prompt = new InputPromptWindow("Создать конфигурацию", "Введите имя новой конфигурации:");
            prompt.Owner = this;
            if (prompt.ShowDialog() == true)
            {
                string name = prompt.InputText;
                if (_settings.Configs.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Конфигурация с таким именем уже существует.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newConfig = new ExportConfig
                {
                    Name = name,
                    ExportFolder = _txtFolder.Text,
                    EachScheduleInSeparateFile = _chkSeparate.IsChecked == true,
                    FilePrefix = _txtPrefix.Text,
                    FileName = _txtFileName.Text,
                    ExportHeader = _chkHeader.IsChecked == true,
                    PreserveFormatting = _chkFormatting.IsChecked == true
                };
                _settings.Configs.Add(newConfig);
                _settings.SelectedConfig = name;
                _settings.Save();

                _isUpdatingUi = true;
                LoadConfigs();
                _isUpdatingUi = false;
            }
        }

        private void BtnEditConfig_Click(object sender, RoutedEventArgs e)
        {
            string selectedConfigName = _cbConfigs.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedConfigName)) return;

            var prompt = new InputPromptWindow("Переименовать конфигурацию", "Введите новое имя конфигурации:", selectedConfigName);
            prompt.Owner = this;
            if (prompt.ShowDialog() == true)
            {
                string newName = prompt.InputText;
                if (_settings.Configs.Any(c => c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Конфигурация с таким именем уже существует.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = _settings.Configs.FirstOrDefault(c => c.Name == selectedConfigName);
                if (config != null)
                {
                    config.Name = newName;
                    _settings.SelectedConfig = newName;
                    _settings.Save();

                    _isUpdatingUi = true;
                    LoadConfigs();
                    _isUpdatingUi = false;
                }
            }
        }

        private void BtnDeleteConfig_Click(object sender, RoutedEventArgs e)
        {
            string selectedConfigName = _cbConfigs.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedConfigName)) return;

            var res = MessageBox.Show($"Вы уверены, что хотите удалить конфигурацию '{selectedConfigName}'?", "Удаление конфигурации", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                var config = _settings.Configs.FirstOrDefault(c => c.Name == selectedConfigName);
                if (config != null)
                {
                    _settings.Configs.Remove(config);
                    _settings.SelectedConfig = "";
                    _settings.Save();

                    _isUpdatingUi = true;
                    LoadConfigs();
                    _cbConfigs.SelectedIndex = -1;
                    _isUpdatingUi = false;
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для экспорта спецификаций";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtFolder.Text = dialog.SelectedPath;
                }
            }
        }

        // Save settings to current configuration before returning
        private void SaveCurrentSettingsToActiveConfig()
        {
            _settings.ExportFolder = _txtFolder.Text;
            _settings.EachScheduleInSeparateFile = _chkSeparate.IsChecked == true;
            _settings.FilePrefix = _txtPrefix.Text;
            _settings.FileName = _txtFileName.Text;
            _settings.ExportHeader = _chkHeader.IsChecked == true;
            _settings.PreserveFormatting = _chkFormatting.IsChecked == true;

            string selectedConfigName = _cbConfigs.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedConfigName))
            {
                var config = _settings.Configs.FirstOrDefault(c => c.Name == selectedConfigName);
                if (config != null)
                {
                    config.ExportFolder = _txtFolder.Text;
                    config.EachScheduleInSeparateFile = _chkSeparate.IsChecked == true;
                    config.FilePrefix = _txtPrefix.Text;
                    config.FileName = _txtFileName.Text;
                    config.ExportHeader = _chkHeader.IsChecked == true;
                    config.PreserveFormatting = _chkFormatting.IsChecked == true;
                }
            }
            _settings.Save();
        }

        private void BtnExportCurrent_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettingsToActiveConfig();
            ExportCurrentOnly = true;
            DialogResult = true;
            Close();
        }

        private void BtnExportAll_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSchedules.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы одну спецификацию для экспорта.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_txtFolder.Text) || !Directory.Exists(_txtFolder.Text))
            {
                MessageBox.Show("Указанная папка экспорта не существует. Пожалуйста, выберите папку.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveCurrentSettingsToActiveConfig();
            ExportCurrentOnly = false;
            DialogResult = true;
            Close();
        }
    }
}
