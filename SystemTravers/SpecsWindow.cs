using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class SpecsWindow : Window
    {
        // Категории
        private CheckBox _chkDucts;
        private CheckBox _chkPipes;
        private CheckBox _chkDuctFittings;
        private CheckBox _chkPipeFittings;
        private CheckBox _chkDuctAccessories;
        private CheckBox _chkPipeAccessories;
        private CheckBox _chkEquipment;
        private CheckBox _chkPlumbing;

        // Группировка
        private CheckBox _chkGroupBySystem;
        private CheckBox _chkGroupBySize;

        // Режим работы
        private RadioButton _rbReadOnly;
        private RadioButton _rbWriteBack;

        // Сопоставление параметров (9 граф ГОСТ)
        private TextBox _txtParamPosition;
        private TextBox _txtParamName;
        private TextBox _txtParamMark;
        private TextBox _txtParamCode;
        private TextBox _txtParamManufacturer;
        private TextBox _txtParamUnit;
        private TextBox _txtParamQuantity;
        private TextBox _txtParamWeight;
        private TextBox _txtParamNote;

        private TextBox _txtFilePath;
        private Button _btnGenerate;

        // Свойства для считывания из внешней команды
        public bool IncludeDucts => _chkDucts.IsChecked == true;
        public bool IncludePipes => _chkPipes.IsChecked == true;
        public bool IncludeDuctFittings => _chkDuctFittings.IsChecked == true;
        public bool IncludePipeFittings => _chkPipeFittings.IsChecked == true;
        public bool IncludeDuctAccessories => _chkDuctAccessories.IsChecked == true;
        public bool IncludePipeAccessories => _chkPipeAccessories.IsChecked == true;
        public bool IncludeEquipment => _chkEquipment.IsChecked == true;
        public bool IncludePlumbing => _chkPlumbing.IsChecked == true;

        public bool GroupBySystem => _chkGroupBySystem.IsChecked == true;
        public bool GroupBySize => _chkGroupBySize.IsChecked == true;

        public bool WriteBackToModel => _rbWriteBack.IsChecked == true;

        public string ParamPosition => _txtParamPosition.Text;
        public string ParamName => _txtParamName.Text;
        public string ParamMark => _txtParamMark.Text;
        public string ParamCode => _txtParamCode.Text;
        public string ParamManufacturer => _txtParamManufacturer.Text;
        public string ParamUnit => _txtParamUnit.Text;
        public string ParamQuantity => _txtParamQuantity.Text;
        public string ParamWeight => _txtParamWeight.Text;
        public string ParamNote => _txtParamNote.Text;

        public string OutputFilePath => _txtFilePath.Text;
        public bool IsGenerationRequested { get; private set; } = false;

        public SpecsWindow()
        {
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
            Title = "Умные спецификации MEP по ГОСТ 21.110 - BimboClub Tools";
            Width = 560;
            Height = 650;
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
            SolidColorBrush textMuted = new SolidColorBrush(Color.FromRgb(160, 160, 170));

            // Общие стили
            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, controlBg));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(5, 0, 5, 0)));
            textBoxStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style checkBoxStyle = new Style(typeof(CheckBox));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brushes.White));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.MarginProperty, new Thickness(0, 4, 0, 4)));

            Style radioButtonStyle = new Style(typeof(RadioButton));
            radioButtonStyle.Setters.Add(new Setter(RadioButton.ForegroundProperty, Brushes.White));
            radioButtonStyle.Setters.Add(new Setter(RadioButton.VerticalAlignmentProperty, VerticalAlignment.Center));
            radioButtonStyle.Setters.Add(new Setter(RadioButton.MarginProperty, new Thickness(0, 4, 0, 4)));

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главный грид
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Вкладки
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });   // Файл выгрузки
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });    // Подвал с кнопками
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
                Text = "СПЕЦИФИКАЦИЯ ОБОРУДОВАНИЯ, ИЗДЕЛИЙ И МАТЕРИАЛОВ (ГОСТ 21.110)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Вкладки управления (TabControl)
            TabControl tabControl = new TabControl
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(15, 15, 15, 5),
                Padding = new Thickness(10)
            };
            Grid.SetRow(tabControl, 1);
            mainGrid.Children.Add(tabControl);

            // Стилизация вкладок
            Style tabItemStyle = new Style(typeof(TabItem));
            tabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, controlBg));
            tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            tabItemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(10, 5, 10, 5)));

            Trigger selectedTabTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selectedTabTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, activeBorderBrush));
            tabItemStyle.Triggers.Add(selectedTabTrigger);

            tabControl.Resources.Add(typeof(TabItem), tabItemStyle);

            // Вкладка 1: Выбор категорий и группировки
            TabItem tab1 = new TabItem { Header = "Категории и режимы" };
            tabControl.Items.Add(tab1);

            Grid tab1Grid = new Grid();
            tab1Grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(145) }); // Категории
            tab1Grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });  // Группировка
            tab1Grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Режим заполнения
            tab1.Content = tab1Grid;

            // 1.1 Категории элементов
            StackPanel catStack = new StackPanel { Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(catStack, 0);
            tab1Grid.Children.Add(catStack);

            TextBlock lblCatTitle = new TextBlock { Text = "ВЫБЕРИТЕ КАТЕГОРИИ ДЛЯ СПЕЦИФИКАЦИИ:", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8), FontSize = 11 };
            catStack.Children.Add(lblCatTitle);

            Grid catGrid = new Grid();
            catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            catStack.Children.Add(catGrid);

            StackPanel leftCat = new StackPanel();
            _chkDucts = new CheckBox { Style = checkBoxStyle, Content = "Воздуховоды", IsChecked = true };
            _chkDuctFittings = new CheckBox { Style = checkBoxStyle, Content = "Фитинги воздуховодов", IsChecked = true };
            _chkDuctAccessories = new CheckBox { Style = checkBoxStyle, Content = "Арматура воздуховодов", IsChecked = true };
            _chkEquipment = new CheckBox { Style = checkBoxStyle, Content = "Оборудование MEP", IsChecked = true };
            leftCat.Children.Add(_chkDucts);
            leftCat.Children.Add(_chkDuctFittings);
            leftCat.Children.Add(_chkDuctAccessories);
            leftCat.Children.Add(_chkEquipment);
            Grid.SetColumn(leftCat, 0);
            catGrid.Children.Add(leftCat);

            StackPanel rightCat = new StackPanel();
            _chkPipes = new CheckBox { Style = checkBoxStyle, Content = "Трубопроводы", IsChecked = true };
            _chkPipeFittings = new CheckBox { Style = checkBoxStyle, Content = "Фитинги трубопроводов", IsChecked = true };
            _chkPipeAccessories = new CheckBox { Style = checkBoxStyle, Content = "Арматура трубопроводов", IsChecked = true };
            _chkPlumbing = new CheckBox { Style = checkBoxStyle, Content = "Сантехприборы", IsChecked = true };
            rightCat.Children.Add(_chkPipes);
            rightCat.Children.Add(_chkPipeFittings);
            rightCat.Children.Add(_chkPipeAccessories);
            rightCat.Children.Add(_chkPlumbing);
            Grid.SetColumn(rightCat, 1);
            catGrid.Children.Add(rightCat);

            // 1.2 Группировка
            StackPanel groupStack = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(groupStack, 1);
            tab1Grid.Children.Add(groupStack);

            TextBlock lblGroupTitle = new TextBlock { Text = "НАСТРОЙКИ ГРУППИРОВКИ В EXCEL:", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), FontSize = 11 };
            groupStack.Children.Add(lblGroupTitle);

            _chkGroupBySystem = new CheckBox { Style = checkBoxStyle, Content = "Группировать раздельно по инженерным системам", IsChecked = true };
            _chkGroupBySize = new CheckBox { Style = checkBoxStyle, Content = "Детализировать строки по типоразмерам (размерам)", IsChecked = true };
            groupStack.Children.Add(_chkGroupBySystem);
            groupStack.Children.Add(_chkGroupBySize);

            // 1.3 Режим работы (запись в модель)
            StackPanel modeStack = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
            Grid.SetRow(modeStack, 2);
            tab1Grid.Children.Add(modeStack);

            TextBlock lblModeTitle = new TextBlock { Text = "РЕЖИМ ОБРАБОТКИ ПАРАМЕТРОВ:", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), FontSize = 11 };
            modeStack.Children.Add(lblModeTitle);

            _rbReadOnly = new RadioButton { Style = radioButtonStyle, Content = "Только экспорт (считать текущие параметры из модели)", IsChecked = true };
            _rbWriteBack = new RadioButton { Style = radioButtonStyle, Content = "Записать расчетное кол-во и ед.изм. в параметры элементов модели" };
            modeStack.Children.Add(_rbReadOnly);
            modeStack.Children.Add(_rbWriteBack);


            // Вкладка 2: Настройка маппинга параметров (9 граф ГОСТ)
            TabItem tab2 = new TabItem { Header = "Параметры ГОСТ" };
            tabControl.Items.Add(tab2);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            tab2.Content = scroll;

            StackPanel paramStack = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };
            scroll.Content = paramStack;

            TextBlock lblParamTitle = new TextBlock { Text = "СОПОСТАВЛЕНИЕ ПАРАМЕТРОВ ДЛЯ КОЛОНОК ГОСТ:", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10), FontSize = 11 };
            paramStack.Children.Add(lblParamTitle);

            // Сетка для сопоставления
            Grid mappingGrid = new Grid();
            mappingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            mappingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 9; i++)
            {
                mappingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            }
            paramStack.Children.Add(mappingGrid);

            // 2.1 Поз.
            AddMappingRow(mappingGrid, 0, "Графа 1: Поз. (Позиция):", ref _txtParamPosition, "ADSK_Позиция", textBoxStyle);
            // 2.2 Наименование
            AddMappingRow(mappingGrid, 1, "Графа 2: Наименование:", ref _txtParamName, "ADSK_Наименование", textBoxStyle);
            // 2.3 Марка
            AddMappingRow(mappingGrid, 2, "Графа 3: Марка / Тип:", ref _txtParamMark, "ADSK_Марка", textBoxStyle);
            // 2.4 Код изделия
            AddMappingRow(mappingGrid, 3, "Графа 4: Код оборудования:", ref _txtParamCode, "ADSK_Код изделия", textBoxStyle);
            // 2.5 Изготовитель
            AddMappingRow(mappingGrid, 4, "Графа 5: Завод-изготовитель:", ref _txtParamManufacturer, "ADSK_Завод-изготовитель", textBoxStyle);
            // 2.6 Ед. изм.
            AddMappingRow(mappingGrid, 5, "Графа 6: Ед. измерения:", ref _txtParamUnit, "ADSK_Единица измерения", textBoxStyle);
            // 2.7 Количество
            AddMappingRow(mappingGrid, 6, "Графа 7: Количество:", ref _txtParamQuantity, "ADSK_Количество", textBoxStyle);
            // 2.8 Масса
            AddMappingRow(mappingGrid, 7, "Графа 8: Масса ед., кг:", ref _txtParamWeight, "ADSK_Масса", textBoxStyle);
            // 2.9 Примечание
            AddMappingRow(mappingGrid, 8, "Графа 9: Примечание:", ref _txtParamNote, "ADSK_Примечание", textBoxStyle);


            // 3. Путь выгрузки
            Border fileBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Margin = new Thickness(15, 5, 15, 10)
            };
            Grid.SetRow(fileBorder, 2);
            mainGrid.Children.Add(fileBorder);

            Grid fileGrid = new Grid();
            fileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            fileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            fileBorder.Child = fileGrid;

            TextBlock lblFileTitle = new TextBlock
            {
                Text = "ПАПКА ЭКСПОРТА И ФАЙЛ EXCEL:",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            fileGrid.Children.Add(lblFileTitle);

            Grid pathGrid = new Grid();
            Grid.SetRow(pathGrid, 1);
            fileGrid.Children.Add(pathGrid);
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });

            _txtFilePath = new TextBox
            {
                Style = textBoxStyle,
                Height = 24,
                IsReadOnly = true,
                Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Спецификация_BimboClub_ГОСТ.xls"),
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(_txtFilePath, 0);
            pathGrid.Children.Add(_txtFilePath);

            Button btnBrowse = new Button { Content = "Обзор...", Foreground = Brushes.White, BorderThickness = new Thickness(0), Height = 24, Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnBrowse, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnBrowse.Click += BtnBrowse_Click;
            Grid.SetColumn(btnBrowse, 1);
            pathGrid.Children.Add(btnBrowse);

            // 4. Подвал с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 3);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка экспорта
            _btnGenerate = new Button
            {
                Content = "Создать спецификацию",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnGenerate.Click += BtnGenerate_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnGenerate, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnGenerate, 1);
            bottomGrid.Children.Add(_btnGenerate);

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

        private void AddMappingRow(Grid grid, int row, string labelText, ref TextBox textBox, string defaultValue, Style textBoxStyle)
        {
            TextBlock lbl = new TextBlock
            {
                Text = labelText,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            textBox = new TextBox
            {
                Style = textBoxStyle,
                Text = defaultValue,
                Height = 22,
                Margin = new Thickness(0, 3, 0, 3)
            };
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Title = "Укажите файл для сохранения спецификации Excel по ГОСТ";
                dialog.Filter = "Книга Excel 97-2003 (*.xls)|*.xls";
                dialog.FileName = "Спецификация_BimboClub_ГОСТ.xls";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtFilePath.Text = dialog.FileName;
                }
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (!IncludeDucts && !IncludePipes && !IncludeDuctFittings && !IncludePipeFittings &&
                !IncludeDuctAccessories && !IncludePipeAccessories && !IncludeEquipment && !IncludePlumbing)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы одну категорию для спецификации.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(ParamPosition) || string.IsNullOrEmpty(ParamName) ||
                string.IsNullOrEmpty(ParamMark) || string.IsNullOrEmpty(ParamCode) ||
                string.IsNullOrEmpty(ParamManufacturer) || string.IsNullOrEmpty(ParamUnit) ||
                string.IsNullOrEmpty(ParamQuantity) || string.IsNullOrEmpty(ParamWeight) ||
                string.IsNullOrEmpty(ParamNote))
            {
                MessageBox.Show("Пожалуйста, убедитесь, что все сопоставления параметров ГОСТ заполнены.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsGenerationRequested = true;
            DialogResult = true;
            Close();
        }
    }
}
