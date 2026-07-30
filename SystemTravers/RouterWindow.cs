using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class RouterWindow : Window
    {
        private ComboBox _cbDirection;
        private ComboBox _cbAngle;
        private TextBox _txtClearance;
        private TextBox _txtOffsetHeight;
        private CheckBox _chkLinks;
        private Button _btnRun;

        private CheckBox _chkWalls;
        private CheckBox _chkFloors;
        private CheckBox _chkColumns;
        private CheckBox _chkBeams;
        private CheckBox _chkDucts;
        private CheckBox _chkPipes;
        private CheckBox _chkCableTrays;

        public string SelectedDirection => ((ComboBoxItem)_cbDirection.SelectedItem).Tag.ToString();
        public double SelectedAngle => double.Parse(((ComboBoxItem)_cbAngle.SelectedItem).Tag.ToString());
        public double ClearanceMm => double.TryParse(_txtClearance.Text, out double val) ? val : 100.0;
        public double OffsetMm => double.TryParse(_txtOffsetHeight.Text, out double val) ? val : 300.0;
        public bool SearchInLinks => _chkLinks.IsChecked == true;
        public bool IsExecutionRequested { get; private set; } = false;

        public System.Collections.Generic.List<Autodesk.Revit.DB.BuiltInCategory> SelectedCategories
        {
            get
            {
                var list = new System.Collections.Generic.List<Autodesk.Revit.DB.BuiltInCategory>();
                if (_chkWalls.IsChecked == true) list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_Walls);
                if (_chkFloors.IsChecked == true) list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_Floors);
                if (_chkColumns.IsChecked == true)
                {
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_StructuralColumns);
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_Columns);
                }
                if (_chkBeams.IsChecked == true) list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_StructuralFraming);
                if (_chkDucts.IsChecked == true)
                {
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_DuctCurves);
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_DuctFitting);
                }
                if (_chkPipes.IsChecked == true)
                {
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves);
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_PipeFitting);
                }
                if (_chkCableTrays.IsChecked == true)
                {
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_CableTray);
                    list.Add(Autodesk.Revit.DB.BuiltInCategory.OST_CableTrayFitting);
                }
                return list;
            }
        }

        public RouterWindow()
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
            Title = "Умная трассировка MEP - BimboClub Tools";
            Width = 440;
            Height = 565;
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

            // Главный грид
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Основной контент
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
                Text = "АВТОМАТИЧЕСКИЙ ОБХОД КОЛЛИЗИЙ («УТКА»)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Основная панель настроек
            Border settingsBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Margin = new Thickness(15, 15, 15, 10)
            };
            Grid.SetRow(settingsBorder, 1);
            mainGrid.Children.Add(settingsBorder);

            Grid settingsGrid = new Grid();
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Заголовок группы
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // Направление
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // Угол
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // Зазор
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // Высота смещения
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Чекбокс связей
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Заголовок категорий
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Грид категорий
            settingsBorder.Child = settingsGrid;

            TextBlock lblGroupTitle = new TextBlock
            {
                Text = "ПАРАМЕТРЫ ТРАССИРОВКИ ОБХОДА:",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            settingsGrid.Children.Add(lblGroupTitle);

            // 2.1 Направление
            Grid dirGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(dirGrid, 1);
            settingsGrid.Children.Add(dirGrid);
            dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblDir = new TextBlock { Text = "Направление смещения:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblDir, 0);
            dirGrid.Children.Add(lblDir);

            _cbDirection = new ComboBox { Style = comboBoxStyle, Height = 24 };
            UiThemeHelper.StyleComboBoxDark(_cbDirection, controlBg, borderBrush);
            _cbDirection.Items.Add(new ComboBoxItem { Content = "Вверх ⬆️", Tag = "UP", IsSelected = true });
            _cbDirection.Items.Add(new ComboBoxItem { Content = "Вниз ⬇️", Tag = "DOWN" });
            _cbDirection.Items.Add(new ComboBoxItem { Content = "Влево ⬅️", Tag = "LEFT" });
            _cbDirection.Items.Add(new ComboBoxItem { Content = "Вправо ➡️", Tag = "RIGHT" });
            Grid.SetColumn(_cbDirection, 1);
            dirGrid.Children.Add(_cbDirection);

            // 2.2 Угол
            Grid angleGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(angleGrid, 2);
            settingsGrid.Children.Add(angleGrid);
            angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblAngle = new TextBlock { Text = "Угол отводов обхода:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblAngle, 0);
            angleGrid.Children.Add(lblAngle);

            _cbAngle = new ComboBox { Style = comboBoxStyle, Height = 24 };
            UiThemeHelper.StyleComboBoxDark(_cbAngle, controlBg, borderBrush);
            _cbAngle.Items.Add(new ComboBoxItem { Content = "30 градусов", Tag = "30" });
            _cbAngle.Items.Add(new ComboBoxItem { Content = "45 градусов", Tag = "45", IsSelected = true });
            _cbAngle.Items.Add(new ComboBoxItem { Content = "90 градусов", Tag = "90" });
            Grid.SetColumn(_cbAngle, 1);
            angleGrid.Children.Add(_cbAngle);

            // 2.3 Зазор
            Grid clearanceGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(clearanceGrid, 3);
            settingsGrid.Children.Add(clearanceGrid);
            clearanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            clearanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblClearance = new TextBlock { Text = "Зазор от препятствия (мм):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblClearance, 0);
            clearanceGrid.Children.Add(lblClearance);

            _txtClearance = new TextBox { Style = textBoxStyle, Text = "100", Height = 24 };
            Grid.SetColumn(_txtClearance, 1);
            clearanceGrid.Children.Add(_txtClearance);

            // 2.4 Высота смещения
            Grid offsetGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(offsetGrid, 4);
            settingsGrid.Children.Add(offsetGrid);
            offsetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            offsetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblOffset = new TextBlock { Text = "Расстояние сдвига (мм):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblOffset, 0);
            offsetGrid.Children.Add(lblOffset);

            _txtOffsetHeight = new TextBox { Style = textBoxStyle, Text = "300", Height = 24 };
            Grid.SetColumn(_txtOffsetHeight, 1);
            offsetGrid.Children.Add(_txtOffsetHeight);

            // 2.5 Чекбокс связей
            _chkLinks = new CheckBox
            {
                Style = checkBoxStyle,
                Content = "Проверять коллизии в связанных файлах (Links)",
                IsChecked = true,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(_chkLinks, 5);
            settingsGrid.Children.Add(_chkLinks);

            // 2.6 Заголовок и кнопки категорий препятствий
            Grid catHeaderGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            catHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            catHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
            Grid.SetRow(catHeaderGrid, 6);
            settingsGrid.Children.Add(catHeaderGrid);

            TextBlock lblCatTitle = new TextBlock
            {
                Text = "ОБХОДИМЫЕ ПРЕПЯТСТВИЯ:",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblCatTitle, 0);
            catHeaderGrid.Children.Add(lblCatTitle);

            StackPanel catSelectStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(catSelectStack, 1);
            catHeaderGrid.Children.Add(catSelectStack);

            Button btnSelectAllCats = new Button { Content = "Выбрать все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Width = 75, Height = 20, FontSize = 10, Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnSelectAllCats, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnSelectAllCats.Click += (s, e) => SetCategoriesChecked(true);
            catSelectStack.Children.Add(btnSelectAllCats);

            Button btnDeselectAllCats = new Button { Content = "Снять все", Foreground = Brushes.White, BorderThickness = new Thickness(0), Width = 75, Height = 20, FontSize = 10, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
            ApplyFlatButtonStyles(btnDeselectAllCats, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            btnDeselectAllCats.Click += (s, e) => SetCategoriesChecked(false);
            catSelectStack.Children.Add(btnDeselectAllCats);

            // 2.7 Грид чекбоксов категорий
            Grid catGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            catGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            catGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            catGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            catGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            Grid.SetRow(catGrid, 7);
            settingsGrid.Children.Add(catGrid);

            _chkWalls = new CheckBox { Style = checkBoxStyle, Content = "Стены", IsChecked = true };
            _chkFloors = new CheckBox { Style = checkBoxStyle, Content = "Перекрытия", IsChecked = true };
            _chkColumns = new CheckBox { Style = checkBoxStyle, Content = "Колонны", IsChecked = true };
            _chkBeams = new CheckBox { Style = checkBoxStyle, Content = "Балки", IsChecked = true };
            _chkDucts = new CheckBox { Style = checkBoxStyle, Content = "Воздуховоды", IsChecked = true };
            _chkPipes = new CheckBox { Style = checkBoxStyle, Content = "Трубы", IsChecked = true };
            _chkCableTrays = new CheckBox { Style = checkBoxStyle, Content = "Кабель-каналы / Лотки", IsChecked = true };

            Grid.SetRow(_chkWalls, 0); Grid.SetColumn(_chkWalls, 0); catGrid.Children.Add(_chkWalls);
            Grid.SetRow(_chkFloors, 0); Grid.SetColumn(_chkFloors, 1); catGrid.Children.Add(_chkFloors);
            Grid.SetRow(_chkColumns, 1); Grid.SetColumn(_chkColumns, 0); catGrid.Children.Add(_chkColumns);
            Grid.SetRow(_chkBeams, 1); Grid.SetColumn(_chkBeams, 1); catGrid.Children.Add(_chkBeams);
            Grid.SetRow(_chkDucts, 2); Grid.SetColumn(_chkDucts, 0); catGrid.Children.Add(_chkDucts);
            Grid.SetRow(_chkPipes, 2); Grid.SetColumn(_chkPipes, 1); catGrid.Children.Add(_chkPipes);
            Grid.SetRow(_chkCableTrays, 3); Grid.SetColumn(_chkCableTrays, 0); catGrid.Children.Add(_chkCableTrays);

            // 3. Подвал с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка запуска
            _btnRun = new Button
            {
                Content = "Проложить обход",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnRun.Click += BtnRun_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnRun, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnRun, 1);
            bottomGrid.Children.Add(_btnRun);

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

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (ClearanceMm <= 0)
            {
                MessageBox.Show("Пожалуйста, укажите положительный зазор.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (OffsetMm <= 0)
            {
                MessageBox.Show("Пожалуйста, укажите положительную высоту смещения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsExecutionRequested = true;
            DialogResult = true;
            Close();
        }

        private void SetCategoriesChecked(bool isChecked)
        {
            _chkWalls.IsChecked = isChecked;
            _chkFloors.IsChecked = isChecked;
            _chkColumns.IsChecked = isChecked;
            _chkBeams.IsChecked = isChecked;
            _chkDucts.IsChecked = isChecked;
            _chkPipes.IsChecked = isChecked;
            _chkCableTrays.IsChecked = isChecked;
        }
    }
}
