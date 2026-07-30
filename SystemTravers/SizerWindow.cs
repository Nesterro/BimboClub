using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;


namespace BimboClub
{
    public class SizerWindow : Window
    {
        // Режимы
        public enum SystemType { Duct, Pipe }
        public enum ShapeType { Round, Rectangular }

        private SystemType _currentSystemType = SystemType.Duct;
        private ShapeType _currentShapeType = ShapeType.Round;

        // Элементы UI
        private RadioButton _rbDuct;
        private RadioButton _rbPipe;
        private RadioButton _rbRound;
        private RadioButton _rbRect;

        private TextBox _txtFlow;
        private Label _lblFlowUnit;
        private Slider _sliderVelocity;
        private TextBox _txtVelocity;
        private Label _lblVelUnit;

        // Круглые результаты
        private TextBlock _txtCalcDiameter;
        private ComboBox _cbStdDiameter;

        // Прямоугольные результаты
        private Grid _rectGrid;
        private RadioButton _rbLockWidth;
        private RadioButton _rbLockHeight;
        private TextBox _txtLockedDimension;
        private TextBlock _txtCalcFreeDimension;
        private ComboBox _cbStdFreeDimension;
        private Label _lblLockDim;
        private Label _lblFreeDim;

        // Реальные параметры
        private TextBlock _txtActualVelocity;
        private TextBlock _txtActualFlow;

        // Кнопки
        private Button _btnApply;
        private TextBlock _txtSelectedCount;

        // Данные от Revit
        private bool _hasSelectedElements = false;
        private string _elementCategory = "";
        
        // Результаты для внешней команды
        public double FinalWidth { get; private set; }
        public double FinalHeight { get; private set; }
        public double FinalDiameter { get; private set; }
        public bool IsApplied { get; private set; } = false;

        public SizerWindow(bool hasSelection, string categoryName, double initialFlow, double initialSizeVal1, double initialSizeVal2)
        {
            _hasSelectedElements = hasSelection;
            _elementCategory = categoryName;

            InitializeComponent();

            // Инициализация значений из выбранных элементов
            if (_hasSelectedElements)
            {
                if (categoryName.Contains("Duct") || categoryName.Contains("воздух"))
                {
                    SetSystemType(SystemType.Duct);
                    if (initialSizeVal2 > 0) // прямоугольный
                    {
                        SetShapeType(ShapeType.Rectangular);
                        _txtLockedDimension.Text = initialSizeVal1.ToString("F0");
                    }
                    else
                    {
                        SetShapeType(ShapeType.Round);
                        _cbStdDiameter.Text = initialSizeVal1.ToString("F0");
                    }
                }
                else // трубы
                {
                    SetSystemType(SystemType.Pipe);
                    SetShapeType(ShapeType.Round);
                    _cbStdDiameter.Text = initialSizeVal1.ToString("F0");
                }

                if (initialFlow > 0)
                {
                    _txtFlow.Text = initialFlow.ToString("F1");
                }
            }

            Recalculate();
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
            // Окно
            Title = "Быстрый расчёт сечений - BimboClub Tools";
            Width = 550;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;

            // Цвета
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

            Style radioStyle = new Style(typeof(RadioButton));
            radioStyle.Setters.Add(new Setter(RadioButton.ForegroundProperty, Brushes.White));
            radioStyle.Setters.Add(new Setter(RadioButton.VerticalAlignmentProperty, VerticalAlignment.Center));

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главная сетка
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Форма
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) }); // Кнопки
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
                Text = "РАСЧЁТ СЕЧЕНИЙ ВОЗДУХОВОДОВ И ТРУБ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Тело формы
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            StackPanel formStack = new StackPanel { Margin = new Thickness(15, 10, 15, 10) };
            scroll.Content = formStack;

            // Панель 1: Тип системы и геометрия
            Border typeBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };
            formStack.Children.Add(typeBorder);

            Grid typeGrid = new Grid();
            typeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            typeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            typeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            typeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            typeBorder.Child = typeGrid;

            // Дисциплина
            TextBlock lblSystem = new TextBlock { Text = "Тип инженерной сети:", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblSystem, 0); Grid.SetColumn(lblSystem, 0);
            typeGrid.Children.Add(lblSystem);

            StackPanel systemStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _rbDuct = new RadioButton { Style = radioStyle, Content = "Воздуховод (Вентиляция)", IsChecked = true, Margin = new Thickness(0, 0, 15, 0) };
            _rbPipe = new RadioButton { Style = radioStyle, Content = "Трубопровод (Отопление/ВК)" };
            _rbDuct.Checked += (s, e) => SetSystemType(SystemType.Duct);
            _rbPipe.Checked += (s, e) => SetSystemType(SystemType.Pipe);
            systemStack.Children.Add(_rbDuct);
            systemStack.Children.Add(_rbPipe);
            Grid.SetRow(systemStack, 0); Grid.SetColumn(systemStack, 1);
            typeGrid.Children.Add(systemStack);

            // Сечение
            TextBlock lblShape = new TextBlock { Text = "Форма сечения:", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblShape, 1); Grid.SetColumn(lblShape, 0);
            typeGrid.Children.Add(lblShape);

            StackPanel shapeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _rbRound = new RadioButton { Style = radioStyle, Content = "Круглое", IsChecked = true, Margin = new Thickness(0, 0, 15, 0) };
            _rbRect = new RadioButton { Style = radioStyle, Content = "Прямоугольное" };
            _rbRound.Checked += (s, e) => SetShapeType(ShapeType.Round);
            _rbRect.Checked += (s, e) => SetShapeType(ShapeType.Rectangular);
            shapeStack.Children.Add(_rbRound);
            shapeStack.Children.Add(_rbRect);
            Grid.SetRow(shapeStack, 1); Grid.SetColumn(shapeStack, 1);
            typeGrid.Children.Add(shapeStack);

            // Панель 2: Входные параметры (Расход и целевая скорость)
            Border inputBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };
            formStack.Children.Add(inputBorder);

            Grid inputGrid = new Grid();
            inputGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            inputGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            inputGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            inputBorder.Child = inputGrid;

            // Расход
            TextBlock lblFlow = new TextBlock { Text = "Расход среды:", Foreground = Brushes.White, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblFlow, 0); Grid.SetColumn(lblFlow, 0);
            inputGrid.Children.Add(lblFlow);

            _txtFlow = new TextBox { Style = textBoxStyle, Text = "500", Height = 26, Margin = new Thickness(0, 0, 10, 0) };
            _txtFlow.TextChanged += (s, e) => Recalculate();
            Grid.SetRow(_txtFlow, 0); Grid.SetColumn(_txtFlow, 1);
            inputGrid.Children.Add(_txtFlow);

            _lblFlowUnit = new Label { Content = "м³/ч", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_lblFlowUnit, 0); Grid.SetColumn(_lblFlowUnit, 2);
            inputGrid.Children.Add(_lblFlowUnit);

            // Целевая скорость
            TextBlock lblVel = new TextBlock { Text = "Целевая скорость:", Foreground = Brushes.White, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblVel, 1); Grid.SetColumn(lblVel, 0);
            inputGrid.Children.Add(lblVel);

            _txtVelocity = new TextBox { Style = textBoxStyle, Text = "4.0", Height = 26, Margin = new Thickness(0, 0, 10, 0) };
            _txtVelocity.TextChanged += TxtVelocity_TextChanged;
            Grid.SetRow(_txtVelocity, 1); Grid.SetColumn(_txtVelocity, 1);
            inputGrid.Children.Add(_txtVelocity);

            _lblVelUnit = new Label { Content = "м/с", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_lblVelUnit, 1); Grid.SetColumn(_lblVelUnit, 2);
            inputGrid.Children.Add(_lblVelUnit);

            // Слайдер скорости
            _sliderVelocity = new Slider
            {
                Minimum = 1.0,
                Maximum = 12.0,
                Value = 4.0,
                TickFrequency = 0.5,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 10, 5)
            };
            _sliderVelocity.ValueChanged += SliderVelocity_ValueChanged;
            Grid.SetRow(_sliderVelocity, 2); Grid.SetColumn(_sliderVelocity, 1);
            inputGrid.Children.Add(_sliderVelocity);

            // Панель 3: Расчёты
            Border calcBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };
            formStack.Children.Add(calcBorder);

            Grid calcGrid = new Grid();
            calcGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Заголовок раздела
            calcGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // Расчетное значение
            calcGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // Выбор стандартного
            calcGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) }); // Доп. для прямоугольных
            calcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            calcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calcGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            calcBorder.Child = calcGrid;

            TextBlock lblCalcTitle = new TextBlock { Text = "ПОДБОР РАЗМЕРОВ СЕЧЕНИЯ:", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblCalcTitle, 0); Grid.SetColumnSpan(lblCalcTitle, 3);
            calcGrid.Children.Add(lblCalcTitle);

            // 3.1 Результаты для КРУГЛОГО сечения
            TextBlock lblCalcD = new TextBlock { Text = "Точный расчетный Ø:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblCalcD, 1); Grid.SetColumn(lblCalcD, 0);
            calcGrid.Children.Add(lblCalcD);

            _txtCalcDiameter = new TextBlock { Text = "— мм", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_txtCalcDiameter, 1); Grid.SetColumn(_txtCalcDiameter, 1);
            calcGrid.Children.Add(_txtCalcDiameter);

            TextBlock lblStdD = new TextBlock { Text = "Стандартный Ø:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblStdD, 2); Grid.SetColumn(lblStdD, 0);
            calcGrid.Children.Add(lblStdD);

            _cbStdDiameter = new ComboBox { Style = comboBoxStyle, Height = 26, Margin = new Thickness(0, 0, 10, 0) };
            UiThemeHelper.StyleComboBoxDark(_cbStdDiameter, controlBg, borderBrush);
            _cbStdDiameter.SelectionChanged += (s, e) => RecalculateActuals();
            _cbStdDiameter.LostFocus += (s, e) => RecalculateActuals();
            Grid.SetRow(_cbStdDiameter, 2); Grid.SetColumn(_cbStdDiameter, 1);
            calcGrid.Children.Add(_cbStdDiameter);

            Label lblUnitMm = new Label { Content = "мм", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblUnitMm, 2); Grid.SetColumn(lblUnitMm, 2);
            calcGrid.Children.Add(lblUnitMm);

            // 3.2 Результаты для ПРЯМОУГОЛЬНОГО сечения (Изначально скрыты)
            _rectGrid = new Grid { Visibility = Visibility.Collapsed };
            _rectGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Фиксация
            _rectGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Задание стороны
            _rectGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Расчетная свободная сторона
            _rectGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Выбор стандартного
            _rectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            _rectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _rectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            Grid.SetRow(_rectGrid, 3); Grid.SetColumnSpan(_rectGrid, 3);
            calcGrid.Children.Add(_rectGrid);

            TextBlock lblFix = new TextBlock { Text = "Зафиксировать размер:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblFix, 0); Grid.SetColumn(lblFix, 0);
            _rectGrid.Children.Add(lblFix);

            StackPanel fixStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _rbLockWidth = new RadioButton { Style = radioStyle, Content = "Ширину (A)", IsChecked = true, Margin = new Thickness(0, 0, 15, 0) };
            _rbLockHeight = new RadioButton { Style = radioStyle, Content = "Высоту (B)" };
            _rbLockWidth.Checked += (s, e) => { UpdateRectLabels(); Recalculate(); };
            _rbLockHeight.Checked += (s, e) => { UpdateRectLabels(); Recalculate(); };
            fixStack.Children.Add(_rbLockWidth);
            fixStack.Children.Add(_rbLockHeight);
            Grid.SetRow(fixStack, 0); Grid.SetColumn(fixStack, 1);
            _rectGrid.Children.Add(fixStack);

            _lblLockDim = new Label { Content = "Ширина (фиксир.), мм:", Foreground = Brushes.White, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_lblLockDim, 1); Grid.SetColumn(_lblLockDim, 0);
            _rectGrid.Children.Add(_lblLockDim);

            _txtLockedDimension = new TextBox { Style = textBoxStyle, Text = "300", Height = 26, Margin = new Thickness(0, 0, 10, 0) };
            _txtLockedDimension.TextChanged += (s, e) => Recalculate();
            Grid.SetRow(_txtLockedDimension, 1); Grid.SetColumn(_txtLockedDimension, 1);
            _rectGrid.Children.Add(_txtLockedDimension);

            Label lblRectUnit1 = new Label { Content = "мм", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblRectUnit1, 1); Grid.SetColumn(lblRectUnit1, 2);
            _rectGrid.Children.Add(lblRectUnit1);

            _lblFreeDim = new Label { Content = "Высота (расчетная):", Foreground = Brushes.White, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_lblFreeDim, 2); Grid.SetColumn(_lblFreeDim, 0);
            _rectGrid.Children.Add(_lblFreeDim);

            _txtCalcFreeDimension = new TextBlock { Text = "— мм", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_txtCalcFreeDimension, 2); Grid.SetColumn(_txtCalcFreeDimension, 1);
            _rectGrid.Children.Add(_txtCalcFreeDimension);

            TextBlock lblStdFree = new TextBlock { Text = "Выбрать стандартный:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblStdFree, 3); Grid.SetColumn(lblStdFree, 0);
            _rectGrid.Children.Add(lblStdFree);

            _cbStdFreeDimension = new ComboBox { Style = comboBoxStyle, Height = 26, Margin = new Thickness(0, 0, 10, 0) };
            UiThemeHelper.StyleComboBoxDark(_cbStdFreeDimension, controlBg, borderBrush);
            _cbStdFreeDimension.SelectionChanged += (s, e) => RecalculateActuals();
            _cbStdFreeDimension.LostFocus += (s, e) => RecalculateActuals();
            Grid.SetRow(_cbStdFreeDimension, 3); Grid.SetColumn(_cbStdFreeDimension, 1);
            _rectGrid.Children.Add(_cbStdFreeDimension);

            Label lblRectUnit2 = new Label { Content = "мм", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblRectUnit2, 3); Grid.SetColumn(lblRectUnit2, 2);
            _rectGrid.Children.Add(lblRectUnit2);

            // Панель 4: Итоговые аэродинамические показатели
            Border resultsBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };
            formStack.Children.Add(resultsBorder);

            Grid resultsGrid = new Grid();
            resultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Заголовок
            resultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Скорость
            resultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Расход
            resultsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            resultsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            resultsBorder.Child = resultsGrid;

            TextBlock lblResTitle = new TextBlock { Text = "ФАКТИЧЕСКИЕ ПАРАМЕТРЫ СЕТИ:", Foreground = activeBorderBrush, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblResTitle, 0); Grid.SetColumnSpan(lblResTitle, 2);
            resultsGrid.Children.Add(lblResTitle);

            TextBlock lblActV = new TextBlock { Text = "Фактическая скорость среды:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblActV, 1); Grid.SetColumn(lblActV, 0);
            resultsGrid.Children.Add(lblActV);

            _txtActualVelocity = new TextBlock { Text = "— м/с", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_txtActualVelocity, 1); Grid.SetColumn(_txtActualVelocity, 1);
            resultsGrid.Children.Add(_txtActualVelocity);

            TextBlock lblActQ = new TextBlock { Text = "Расход через сечение:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblActQ, 2); Grid.SetColumn(lblActQ, 0);
            resultsGrid.Children.Add(lblActQ);

            _txtActualFlow = new TextBlock { Text = "— м³/ч", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(_txtActualFlow, 2); Grid.SetColumn(_txtActualFlow, 1);
            resultsGrid.Children.Add(_txtActualFlow);


            // 3. Подвал с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка Применить (Алый градиент)
            _btnApply = new Button
            {
                Content = "Применить сечение",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnApply.Click += BtnApply_Click;
            
            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnApply, normalGrad, hoverGrad, roundedButtonStyle);
            bottomGrid.Children.Add(_btnApply);

            // Информационный счетчик
            _txtSelectedCount = new TextBlock
            {
                Text = _hasSelectedElements ? $"В Revit выбрано: {_elementCategory}" : "Калькулятор (автономный режим)",
                Foreground = textMuted,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, -2, 0, 0)
            };
            // Будем выводить описание в первой колонке кнопочного грида
            Grid.SetColumn(_txtSelectedCount, 0);
            _btnApply.ToolTip = _txtSelectedCount.Text;

            // Кнопка Закрыть (Темный)
            Button btnClose = new Button
            {
                Content = "Закрыть",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            
            SolidColorBrush cancelNormal = new SolidColorBrush(Color.FromRgb(58, 58, 66));
            SolidColorBrush cancelHover = new SolidColorBrush(Color.FromRgb(78, 78, 88));
            ApplyFlatButtonStyles(btnClose, cancelNormal, cancelHover, roundedButtonStyle);
            
            Grid.SetColumn(btnClose, 2);
            bottomGrid.Children.Add(btnClose);

            // Подгружаем стандартные ряды размеров
            PopulateDiameterComboBox();
        }

        private void SetSystemType(SystemType sysType)
        {
            _currentSystemType = sysType;

            if (_currentSystemType == SystemType.Duct)
            {
                _lblFlowUnit.Content = "м³/ч";
                _lblVelUnit.Content = "м/с";
                _sliderVelocity.Minimum = 1.0;
                _sliderVelocity.Maximum = 12.0;
                if (_sliderVelocity.Value < 1.0 || _sliderVelocity.Value > 12.0)
                    _sliderVelocity.Value = 4.0;

                _rbRect.IsEnabled = true;
            }
            else // Трубы
            {
                _lblFlowUnit.Content = "л/с";
                _lblVelUnit.Content = "м/с";
                _sliderVelocity.Minimum = 0.1;
                _sliderVelocity.Maximum = 3.0;
                if (_sliderVelocity.Value < 0.1 || _sliderVelocity.Value > 3.0)
                    _sliderVelocity.Value = 1.0;

                // Трубы бывают только круглые в Revit
                _rbRound.IsChecked = true;
                _rbRect.IsEnabled = false;
                SetShapeType(ShapeType.Round);
            }

            PopulateDiameterComboBox();
            Recalculate();
        }

        private void SetShapeType(ShapeType shapeType)
        {
            _currentShapeType = shapeType;

            if (_currentShapeType == ShapeType.Round)
            {
                _txtCalcDiameter.Visibility = Visibility.Visible;
                _cbStdDiameter.Visibility = Visibility.Visible;
                _rectGrid.Visibility = Visibility.Collapsed;
            }
            else // Прямоугольные
            {
                _txtCalcDiameter.Visibility = Visibility.Collapsed;
                _cbStdDiameter.Visibility = Visibility.Collapsed;
                _rectGrid.Visibility = Visibility.Visible;
                UpdateRectLabels();
            }

            Recalculate();
        }

        private void UpdateRectLabels()
        {
            if (_rbLockWidth.IsChecked == true)
            {
                _lblLockDim.Content = "Ширина зафиксир. (A):";
                _lblFreeDim.Content = "Высота расчетная (B):";
            }
            else
            {
                _lblLockDim.Content = "Высота зафиксир. (B):";
                _lblFreeDim.Content = "Ширина расчетная (A):";
            }
        }

        private void PopulateDiameterComboBox()
        {
            _cbStdDiameter.Items.Clear();
            _cbStdFreeDimension.Items.Clear();

            List<int> stdSizes;

            if (_currentSystemType == SystemType.Duct)
            {
                // Стандартные размеры воздуховодов по ГОСТ
                stdSizes = new List<int> { 100, 125, 150, 160, 200, 250, 315, 355, 400, 450, 500, 560, 630, 710, 800, 900, 1000, 1120, 1250 };
            }
            else
            {
                // Стандартные диаметры стальных/пластиковых труб (DN)
                stdSizes = new List<int> { 10, 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300 };
            }

            foreach (var size in stdSizes)
            {
                _cbStdDiameter.Items.Add(size.ToString());
                _cbStdFreeDimension.Items.Add(size.ToString());
            }
        }

        private void SliderVelocity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_txtVelocity != null)
            {
                _txtVelocity.Text = _sliderVelocity.Value.ToString("F1");
                Recalculate();
            }
        }

        private void TxtVelocity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(_txtVelocity.Text, out double vel))
            {
                if (vel >= _sliderVelocity.Minimum && vel <= _sliderVelocity.Maximum)
                {
                    _sliderVelocity.Value = vel;
                    Recalculate();
                }
            }
        }

        private void Recalculate()
        {
            if (_txtFlow == null || _txtVelocity == null) return;

            if (!double.TryParse(_txtFlow.Text, out double flow) || flow <= 0) return;
            if (!double.TryParse(_txtVelocity.Text, out double velocity) || velocity <= 0) return;

            if (_currentSystemType == SystemType.Duct) // ВОЗДУХОВОДЫ
            {
                // Перевод расхода из м3/ч в м3/с: Q_m3s = Q_m3h / 3600
                double flowM3S = flow / 3600.0;
                // Необходимая площадь сечения: A = Q / v
                double reqArea = flowM3S / velocity;

                if (_currentShapeType == ShapeType.Round) // Круглые
                {
                    // d = sqrt(4 * A / pi)
                    double calcDiameterMm = Math.Sqrt((4.0 * reqArea) / Math.PI) * 1000.0;
                    _txtCalcDiameter.Text = $"{calcDiameterMm:F1} мм";

                    // Подбираем ближайший больший стандартный размер
                    int recSize = 100;
                    foreach (var item in _cbStdDiameter.Items)
                    {
                        int size = int.Parse(item.ToString());
                        if (size >= calcDiameterMm)
                        {
                            recSize = size;
                            break;
                        }
                        recSize = size; // если превысил максимальный
                    }
                    _cbStdDiameter.Text = recSize.ToString();
                }
                else // Прямоугольные
                {
                    if (!double.TryParse(_txtLockedDimension.Text, out double lockedDimMm) || lockedDimMm <= 0) return;

                    // Расчет свободной стороны: B = A / (LockedDim / 1000) * 1000
                    double calcFreeMm = (reqArea / (lockedDimMm / 1000.0)) * 1000.0;
                    _txtCalcFreeDimension.Text = $"{calcFreeMm:F1} мм";

                    // Подбираем стандартный
                    int recSize = 100;
                    foreach (var item in _cbStdFreeDimension.Items)
                    {
                        int size = int.Parse(item.ToString());
                        if (size >= calcFreeMm)
                        {
                            recSize = size;
                            break;
                        }
                        recSize = size;
                    }
                    _cbStdFreeDimension.Text = recSize.ToString();
                }
            }
            else // ТРУБОПРОВОДЫ
            {
                // Расход из л/с в м3/с: Q_m3s = Q_ls / 1000
                double flowM3S = flow / 1000.0;
                double reqArea = flowM3S / velocity;

                double calcDiameterMm = Math.Sqrt((4.0 * reqArea) / Math.PI) * 1000.0;
                _txtCalcDiameter.Text = $"{calcDiameterMm:F1} мм";

                // Подбор стандартного DN
                int recSize = 15;
                foreach (var item in _cbStdDiameter.Items)
                {
                    int size = int.Parse(item.ToString());
                    if (size >= calcDiameterMm)
                    {
                        recSize = size;
                        break;
                    }
                    recSize = size;
                }
                _cbStdDiameter.Text = recSize.ToString();
            }

            RecalculateActuals();
        }

        private void RecalculateActuals()
        {
            if (_txtActualVelocity == null || _txtActualFlow == null) return;
            if (!double.TryParse(_txtFlow.Text, out double flow) || flow <= 0) return;

            if (_currentSystemType == SystemType.Duct)
            {
                if (_currentShapeType == ShapeType.Round)
                {
                    if (!double.TryParse(_cbStdDiameter.Text, out double stdD) || stdD <= 0) return;
                    double area = Math.PI * Math.Pow(stdD / 2000.0, 2);
                    double actVel = (flow / 3600.0) / area;

                    _txtActualVelocity.Text = $"{actVel:F2} м/с";
                    _txtActualFlow.Text = $"{flow:F0} м³/ч";
                }
                else
                {
                    if (!double.TryParse(_txtLockedDimension.Text, out double lockedDim) || lockedDim <= 0) return;
                    if (!double.TryParse(_cbStdFreeDimension.Text, out double stdFree) || stdFree <= 0) return;

                    double area = (lockedDim / 1000.0) * (stdFree / 1000.0);
                    double actVel = (flow / 3600.0) / area;

                    _txtActualVelocity.Text = $"{actVel:F2} м/с";
                    _txtActualFlow.Text = $"{flow:F0} м³/ч";
                }
            }
            else // Трубы
            {
                if (!double.TryParse(_cbStdDiameter.Text, out double stdD) || stdD <= 0) return;
                double area = Math.PI * Math.Pow(stdD / 2000.0, 2);
                // Скорость: v = (Q_ls / 1000) / A
                double actVel = (flow / 1000.0) / area;

                _txtActualVelocity.Text = $"{actVel:F2} м/с";
                _txtActualFlow.Text = $"{flow:F2} л/с";
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSelectedElements)
            {
                MessageBox.Show("Режим калькулятора. Выберите элементы в модели Revit перед запуском, чтобы применить размеры.", 
                    "Автономный режим", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_currentSystemType == SystemType.Duct)
            {
                if (_currentShapeType == ShapeType.Round)
                {
                    if (double.TryParse(_cbStdDiameter.Text, out double d))
                        FinalDiameter = d;
                }
                else
                {
                    if (double.TryParse(_txtLockedDimension.Text, out double locked) &&
                        double.TryParse(_cbStdFreeDimension.Text, out double free))
                    {
                        if (_rbLockWidth.IsChecked == true)
                        {
                            FinalWidth = locked;
                            FinalHeight = free;
                        }
                        else
                        {
                            FinalWidth = free;
                            FinalHeight = locked;
                        }
                    }
                }
            }
            else // Трубы
            {
                if (double.TryParse(_cbStdDiameter.Text, out double d))
                    FinalDiameter = d;
            }

            IsApplied = true;
            this.DialogResult = true;
            this.Close();
        }
    }
}
