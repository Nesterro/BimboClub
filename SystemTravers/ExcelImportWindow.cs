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
using Microsoft.Win32;

namespace BimboClub
{
    public class ViewLinkItem
    {
        public string ViewName { get; set; }
        public string ExcelPath { get; set; }
        public string SheetName { get; set; }
        public string CellRange { get; set; }
        public string LastImported { get; set; }
        public object RawView { get; set; }
        public bool IsChecked { get; set; } = false;
    }

    public class ComboBoxItemTag
    {
        public string DisplayText { get; set; }
        public object TagValue { get; set; }
        public override string ToString() => DisplayText;
    }

    public class ExcelImportWindow : Window
    {
        private TextBox _txtFilePath;
        private ComboBox _cbWorksheet;
        private ComboBox _cbRangeType;
        private TextBox _txtRange;
        private ComboBox _cbTargetView;
        private TextBox _txtNewViewName;
        private ListBox _lstExistingLinks;

        private Button _btnImport;
        private Button _btnUpdateSelected;

        public string ExcelPath => _txtFilePath.Text.Trim();
        public string SheetName => _cbWorksheet.SelectedItem?.ToString();

        public string CellRange
        {
            get
            {
                if (_cbRangeType == null) return "";
                if (_cbRangeType.SelectedIndex == 1) // Область печати
                {
                    return _detectedPrintArea ?? "";
                }
                if (_cbRangeType.SelectedIndex == 2) // Пользовательский диапазон
                {
                    return _txtRange.Text.Trim();
                }
                return ""; // Вся заполненная область
            }
        }

        public object SelectedTargetView => (_cbTargetView.SelectedItem as ComboBoxItemTag)?.TagValue;
        public string NewViewName => _txtNewViewName.Text.Trim();
        public bool CreateNewDrafting => (_cbTargetView.SelectedItem as ComboBoxItemTag)?.TagValue?.ToString() == "CREATE_DRAFTING";
        public bool CreateNewLegend => (_cbTargetView.SelectedItem as ComboBoxItemTag)?.TagValue?.ToString() == "CREATE_LEGEND";

        public List<ViewLinkItem> SelectedExistingLinks => _links.Where(l => l.IsChecked).ToList();

        private List<ViewLinkItem> _links;
        private List<object> _availableViews; // List of Tuple<string, object> or dynamic view list
        private Func<string, List<string>> _onFileSelected;
        private Func<string, string, string> _onGetPrintArea;
        private string _detectedPrintArea = null;

        public bool IsImportRequested { get; private set; } = false;
        public bool IsUpdateRequested { get; private set; } = false;

        public ExcelImportWindow(List<ViewLinkItem> links, List<object> availableViews, Func<string, List<string>> onFileSelected, Func<string, string, string> onGetPrintArea)
        {
            _links = links;
            _availableViews = availableViews;
            _onFileSelected = onFileSelected;
            _onGetPrintArea = onGetPrintArea;

            InitializeComponent();
            PopulateTargetViews();
            PopulateExistingLinks();
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
            Title = "Импорт таблиц Excel в Revit - BimboClub Tools";
            Width = 600;
            Height = 740;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            MinHeight = 600;
            MinWidth = 550;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B
            SolidColorBrush textMuted = new SolidColorBrush(Color.FromRgb(160, 160, 170));

            // Стили по умолчанию
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

            Style comboBoxItemStyle = new Style(typeof(ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, controlBg));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(ComboBoxItem), comboBoxItemStyle);

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главная сетка
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Контент
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });    // Подвал
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
                Text = "ИМПОРТ ТАБЛИЦ EXCEL",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Контентная зона (ScrollViewer)
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(15)
            };
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            StackPanel contentStack = new StackPanel();
            scroll.Content = contentStack;

            // --- Блок 1: Источник Excel ---
            GroupBox gbExcel = new GroupBox
            {
                Header = "ИСТОЧНИК EXCEL:",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            contentStack.Children.Add(gbExcel);

            Grid excelGrid = new Grid();
            excelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            excelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            excelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            excelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // 4 rows now
            gbExcel.Content = excelGrid;

            excelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            excelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            excelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });

            // Строка 1: Файл
            TextBlock lblFile = new TextBlock { Text = "Файл Excel:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Normal };
            Grid.SetRow(lblFile, 0); Grid.SetColumn(lblFile, 0);
            excelGrid.Children.Add(lblFile);

            _txtFilePath = new TextBox { Style = textBoxStyle, IsReadOnly = true, Height = 24, Margin = new Thickness(0, 0, 5, 0) };
            Grid.SetRow(_txtFilePath, 0); Grid.SetColumn(_txtFilePath, 1);
            excelGrid.Children.Add(_txtFilePath);

            Button btnBrowse = new Button { Content = "Обзор...", Foreground = Brushes.White, BorderThickness = new Thickness(0), Height = 24, Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnBrowse, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnBrowse.Click += BtnBrowse_Click;
            Grid.SetRow(btnBrowse, 0); Grid.SetColumn(btnBrowse, 2);
            excelGrid.Children.Add(btnBrowse);

            // Строка 2: Лист
            TextBlock lblSheet = new TextBlock { Text = "Лист:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Normal };
            Grid.SetRow(lblSheet, 1); Grid.SetColumn(lblSheet, 0);
            excelGrid.Children.Add(lblSheet);

            _cbWorksheet = new ComboBox { Style = comboBoxStyle, Height = 24, Margin = new Thickness(0, 0, 5, 0) };
            UiThemeHelper.StyleComboBoxDark(_cbWorksheet, controlBg, borderBrush);
            _cbWorksheet.SelectionChanged += CbWorksheet_SelectionChanged;
            Grid.SetRow(_cbWorksheet, 1); Grid.SetColumn(_cbWorksheet, 1); Grid.SetColumnSpan(_cbWorksheet, 2);
            excelGrid.Children.Add(_cbWorksheet);

            // Строка 3: Тип диапазона
            TextBlock lblRangeType = new TextBlock { Text = "Тип диапазона:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Normal };
            Grid.SetRow(lblRangeType, 2); Grid.SetColumn(lblRangeType, 0);
            excelGrid.Children.Add(lblRangeType);

            _cbRangeType = new ComboBox { Style = comboBoxStyle, Height = 24, Margin = new Thickness(0, 0, 5, 0) };
            UiThemeHelper.StyleComboBoxDark(_cbRangeType, controlBg, borderBrush);
            _cbRangeType.Items.Add(new ComboBoxItemTag { DisplayText = "Вся заполненная область", TagValue = "ALL" });
            _cbRangeType.Items.Add(new ComboBoxItemTag { DisplayText = "Область печати (не задана)", TagValue = null });
            _cbRangeType.Items.Add(new ComboBoxItemTag { DisplayText = "Ввести вручную...", TagValue = "CUSTOM" });
            _cbRangeType.SelectedIndex = 0;
            _cbRangeType.SelectionChanged += CbRangeType_SelectionChanged;
            Grid.SetRow(_cbRangeType, 2); Grid.SetColumn(_cbRangeType, 1); Grid.SetColumnSpan(_cbRangeType, 2);
            excelGrid.Children.Add(_cbRangeType);

            // Строка 4: Ввод диапазона
            TextBlock lblRange = new TextBlock { Text = "Диапазон ячеек:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Normal, FontSize = 11 };
            Grid.SetRow(lblRange, 3); Grid.SetColumn(lblRange, 0);
            excelGrid.Children.Add(lblRange);

            _txtRange = new TextBox { Style = textBoxStyle, Height = 24, Margin = new Thickness(0, 0, 5, 0), IsEnabled = false, ToolTip = "Например: A1:D10" };
            Grid.SetRow(_txtRange, 3); Grid.SetColumn(_txtRange, 1); Grid.SetColumnSpan(_txtRange, 2);
            excelGrid.Children.Add(_txtRange);


            // --- Блок 2: Назначение в Revit ---
            GroupBox gbRevit = new GroupBox
            {
                Header = "ЦЕЛЕВОЙ ВИД В REVIT:",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            contentStack.Children.Add(gbRevit);

            Grid revitGrid = new Grid();
            revitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            revitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            gbRevit.Content = revitGrid;

            revitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            revitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Строка 1: Выбор вида
            TextBlock lblTarget = new TextBlock { Text = "Выбрать вид:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Normal };
            Grid.SetRow(lblTarget, 0); Grid.SetColumn(lblTarget, 0);
            revitGrid.Children.Add(lblTarget);

            _cbTargetView = new ComboBox { Style = comboBoxStyle, Height = 24 };
            UiThemeHelper.StyleComboBoxDark(_cbTargetView, controlBg, borderBrush);
            _cbTargetView.SelectionChanged += CbTargetView_SelectionChanged;
            Grid.SetRow(_cbTargetView, 0); Grid.SetColumn(_cbTargetView, 1);
            revitGrid.Children.Add(_cbTargetView);

            // Строка 2: Имя нового вида
            TextBlock lblNewName = new TextBlock { Text = "Имя нового вида:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Normal };
            Grid.SetRow(lblNewName, 1); Grid.SetColumn(lblNewName, 0);
            revitGrid.Children.Add(lblNewName);

            _txtNewViewName = new TextBox { Style = textBoxStyle, Height = 24 };
            Grid.SetRow(_txtNewViewName, 1); Grid.SetColumn(_txtNewViewName, 1);
            revitGrid.Children.Add(_txtNewViewName);


            // --- Блок 3: Связанные таблицы (Для обновления) ---
            GroupBox gbExisting = new GroupBox
            {
                Header = "СВЯЗАННЫЕ ТАБЛИЦЫ В ПРОЕКТЕ (ОБНОВЛЕНИЕ):",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                BorderBrush = borderBrush,
                Height = 220,
                Padding = new Thickness(10)
            };
            contentStack.Children.Add(gbExisting);

            Grid existingGrid = new Grid();
            existingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            existingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            gbExisting.Content = existingGrid;

            _lstExistingLinks = new ListBox
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Background = panelBg,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                SelectionMode = SelectionMode.Single,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(_lstExistingLinks, 0);
            existingGrid.Children.Add(_lstExistingLinks);

            _btnUpdateSelected = new Button { Content = "Обновить выбранные виды", Foreground = Brushes.White, BorderThickness = new Thickness(0), Height = 26, Cursor = Cursors.Hand, FontWeight = FontWeights.Bold };
            ApplyFlatButtonStyles(_btnUpdateSelected, new SolidColorBrush(Color.FromRgb(179, 14, 45)), new SolidColorBrush(Color.FromRgb(210, 20, 60)), roundedButtonStyle);
            _btnUpdateSelected.Click += BtnUpdateSelected_Click;
            Grid.SetRow(_btnUpdateSelected, 1);
            existingGrid.Children.Add(_btnUpdateSelected);


            // 3. Подвал
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка Импорт
            _btnImport = new Button
            {
                Content = "Импортировать",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnImport.Click += BtnImport_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnImport, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnImport, 1);
            bottomGrid.Children.Add(_btnImport);

            // Кнопка Закрыть
            Button btnCancel = new Button
            {
                Content = "Закрыть",
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

        private void PopulateTargetViews()
        {
            _cbTargetView.Items.Clear();

            _cbTargetView.Items.Add(new ComboBoxItemTag { DisplayText = "<Создать новый чертежный вид>", TagValue = "CREATE_DRAFTING" });
            _cbTargetView.Items.Add(new ComboBoxItemTag { DisplayText = "<Создать новую легенду>", TagValue = "CREATE_LEGEND" });

            if (_availableViews != null)
            {
                foreach (var view in _availableViews)
                {
                    var name = view.GetType().GetProperty("Item1")?.GetValue(view)?.ToString() ?? "Вид";
                    var viewObj = view.GetType().GetProperty("Item2")?.GetValue(view);
                    _cbTargetView.Items.Add(new ComboBoxItemTag { DisplayText = name, TagValue = viewObj });
                }
            }

            _cbTargetView.SelectedIndex = 0;
        }

        private void PopulateExistingLinks()
        {
            _lstExistingLinks.Items.Clear();

            if (_links == null || _links.Count == 0)
            {
                _lstExistingLinks.Items.Add(new TextBlock
                {
                    Text = "Нет связанных таблиц в текущем проекте.",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                _btnUpdateSelected.IsEnabled = false;
                return;
            }

            _btnUpdateSelected.IsEnabled = true;

            foreach (var link in _links)
            {
                Grid rowGrid = new Grid { Margin = new Thickness(5, 4, 5, 4) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // Чекбокс
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Имя вида
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Файл + Лист
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Дата

                CheckBox chk = new CheckBox
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = link.IsChecked
                };
                chk.Checked += (s, e) => { link.IsChecked = true; };
                chk.Unchecked += (s, e) => { link.IsChecked = false; };
                Grid.SetColumn(chk, 0);
                rowGrid.Children.Add(chk);

                TextBlock txtName = new TextBlock
                {
                    Text = link.ViewName,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(txtName, 1);
                rowGrid.Children.Add(txtName);

                string excelName = Path.GetFileName(link.ExcelPath);
                TextBlock txtFile = new TextBlock
                {
                    Text = $"{excelName} [{link.SheetName}]",
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 205)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = link.ExcelPath,
                    Margin = new Thickness(5, 0, 5, 0)
                };
                Grid.SetColumn(txtFile, 2);
                rowGrid.Children.Add(txtFile);

                TextBlock txtDate = new TextBlock
                {
                    Text = link.LastImported,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(txtDate, 3);
                rowGrid.Children.Add(txtDate);

                _lstExistingLinks.Items.Add(rowGrid);
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Файлы Excel (*.xlsx;*.xls)|*.xlsx;*.xls",
                Title = "Выберите Excel-файл для импорта"
            };

            if (dialog.ShowDialog() == true)
            {
                _txtFilePath.Text = dialog.FileName;

                try
                {
                    var sheets = _onFileSelected?.Invoke(dialog.FileName);
                    _cbWorksheet.Items.Clear();
                    if (sheets != null)
                    {
                        foreach (var sheet in sheets)
                        {
                            _cbWorksheet.Items.Add(sheet);
                        }
                        if (_cbWorksheet.Items.Count > 0)
                        {
                            _cbWorksheet.SelectedIndex = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при чтении листов из файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CbWorksheet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePrintAreaOption();

            if (_cbTargetView != null && _cbTargetView.SelectedIndex < 2 && _txtNewViewName != null)
            {
                string sheetName = _cbWorksheet.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(sheetName))
                {
                    _txtNewViewName.Text = "Импорт_" + sheetName;
                }
            }
        }

        private void UpdatePrintAreaOption()
        {
            if (_cbWorksheet.SelectedItem == null || string.IsNullOrEmpty(ExcelPath))
            {
                _detectedPrintArea = null;
                if (_cbRangeType != null && _cbRangeType.Items.Count > 1)
                {
                    var item = _cbRangeType.Items[1] as ComboBoxItemTag;
                    if (item != null)
                    {
                        item.DisplayText = "Область печати (не задана)";
                        item.TagValue = null;
                        _cbRangeType.Items[1] = item;
                        _cbRangeType.SelectedIndex = 0;
                    }
                }
                return;
            }

            string sheetName = _cbWorksheet.SelectedItem.ToString();
            try
            {
                _detectedPrintArea = _onGetPrintArea?.Invoke(ExcelPath, sheetName);
            }
            catch
            {
                _detectedPrintArea = null;
            }

            if (_cbRangeType != null && _cbRangeType.Items.Count > 1)
            {
                var item = _cbRangeType.Items[1] as ComboBoxItemTag;
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(_detectedPrintArea))
                    {
                        item.DisplayText = $"Область печати ({_detectedPrintArea})";
                        item.TagValue = _detectedPrintArea;
                        _cbRangeType.Items[1] = item;
                        _cbRangeType.SelectedIndex = 1; // Default to print area if exists
                    }
                    else
                    {
                        item.DisplayText = "Область печати (не задана)";
                        item.TagValue = null;
                        _cbRangeType.Items[1] = item;
                        if (_cbRangeType.SelectedIndex == 1)
                        {
                            _cbRangeType.SelectedIndex = 0;
                        }
                    }
                }
            }
        }

        private void CbRangeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cbRangeType.SelectedIndex == 2) // Ввести вручную...
            {
                _txtRange.IsEnabled = true;
                _txtRange.Focus();
            }
            else
            {
                _txtRange.IsEnabled = false;
                if (_cbRangeType.SelectedIndex == 1) // Область печати
                {
                    _txtRange.Text = _detectedPrintArea ?? "";
                }
                else
                {
                    _txtRange.Text = "";
                }
            }
        }

        private void CbTargetView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cbTargetView.SelectedIndex < 2)
            {
                _txtNewViewName.IsEnabled = true;
                if (string.IsNullOrEmpty(_txtNewViewName.Text) || _txtNewViewName.Text.StartsWith("Импорт_"))
                {
                    string sheetName = _cbWorksheet.SelectedItem?.ToString() ?? "Лист1";
                    _txtNewViewName.Text = "Импорт_" + sheetName;
                }
            }
            else
            {
                _txtNewViewName.IsEnabled = false;
                _txtNewViewName.Text = "";
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ExcelPath) || !File.Exists(ExcelPath))
            {
                MessageBox.Show("Пожалуйста, выберите существующий Excel-файл.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(SheetName))
            {
                MessageBox.Show("Пожалуйста, выберите рабочий лист.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CreateNewDrafting || CreateNewLegend)
            {
                if (string.IsNullOrWhiteSpace(NewViewName))
                {
                    MessageBox.Show("Пожалуйста, укажите имя для создаваемого вида.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            IsImportRequested = true;
            DialogResult = true;
            Close();
        }

        private void BtnUpdateSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedExistingLinks;
            if (selected.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один вид для обновления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsUpdateRequested = true;
            DialogResult = true;
            Close();
        }
    }
}
