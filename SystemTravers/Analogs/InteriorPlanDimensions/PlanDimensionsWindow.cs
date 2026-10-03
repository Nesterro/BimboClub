using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using WpfGrid = System.Windows.Controls.Grid;
using MediaColor = System.Windows.Media.Color;

namespace BimboClub.Analogs.InteriorPlanDimensions
{
    public class PlanDimensionsWindow : Window
    {
        private readonly Document _doc;
        private readonly List<DimensionType> _dimTypes;

        private ComboBox _cbDimTypes;
        private RadioButton _rbSelected;
        private RadioButton _rbActiveView;

        // Стены
        private CheckBox _chkWallChains;
        private CheckBox _chkWallGrids;
        private CheckBox _chkWallThickness;
        private TextBox _tbOffsetMm;
        private TextBox _tbMinWallWidthMm;
        private TextBox _tbExcludeTypes;

        // Колонны
        private CheckBox _chkColumns;
        private CheckBox _chkColGrids;

        public bool ProcessOnlySelected => _rbSelected.IsChecked == true;
        public PlanDimensionsOptions Options { get; private set; }

        public PlanDimensionsWindow(Document doc, bool hasSelection)
        {
            _doc = doc;
            _dimTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(dt => dt.StyleType == DimensionStyleType.Linear)
                .OrderBy(dt => dt.Name)
                .ToList();

            InitializeUi(hasSelection);
        }

        private void InitializeUi(bool hasSelection)
        {
            Title = "Размеры на плане — BimboClub Tools";
            Width = 620;
            Height = 610;
            MinHeight = 520;
            MinWidth = 550;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            var bgCard = new SolidColorBrush(MediaColor.FromRgb(32, 32, 38));
            var borderCard = new SolidColorBrush(MediaColor.FromRgb(48, 48, 56));
            var accentRed = new SolidColorBrush(MediaColor.FromRgb(179, 14, 45));
            var textMuted = new SolidColorBrush(MediaColor.FromRgb(180, 180, 200));

            WpfGrid mainGrid = new WpfGrid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Scope / DimType Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Options Body
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer
            Content = mainGrid;

            // 1. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(MediaColor.FromRgb(18, 18, 20), MediaColor.FromRgb(179, 14, 45), 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            StackPanel headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "РАЗМЕРЫ НА ПЛАНЕ",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Автоматическая расстановка размерных цепочек стен, проёмов, толщин и привязок колонн к осям",
                FontSize = 11,
                Foreground = textMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBorder.Child = headerStack;
            WpfGrid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. Тип размера и область действия
            Border topBar = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 14, 10)
            };
            StackPanel topStack = new StackPanel();

            // Выбор типа размера
            WpfGrid dimTypeGrid = new WpfGrid { Margin = new Thickness(0, 0, 0, 8) };
            dimTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dimTypeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblType = new TextBlock
            {
                Text = "Тип размера:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            WpfGrid.SetColumn(lblType, 0);
            dimTypeGrid.Children.Add(lblType);

            _cbDimTypes = new ComboBox
            {
                ItemsSource = _dimTypes,
                DisplayMemberPath = "Name",
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(6, 3, 6, 3)
            };
            if (_dimTypes.Count > 0) _cbDimTypes.SelectedIndex = 0;
            WpfGrid.SetColumn(_cbDimTypes, 1);
            dimTypeGrid.Children.Add(_cbDimTypes);

            topStack.Children.Add(dimTypeGrid);

            // Область действия
            StackPanel scopeStack = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock lblScope = new TextBlock
            {
                Text = "Обрабатывать:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            scopeStack.Children.Add(lblScope);

            _rbSelected = new RadioButton
            {
                Content = "Выбранные элементы",
                Foreground = Brushes.White,
                IsChecked = hasSelection,
                IsEnabled = hasSelection,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _rbActiveView = new RadioButton
            {
                Content = "Все элементы на активном виде",
                Foreground = Brushes.White,
                IsChecked = !hasSelection,
                VerticalAlignment = VerticalAlignment.Center
            };
            scopeStack.Children.Add(_rbSelected);
            scopeStack.Children.Add(_rbActiveView);

            topStack.Children.Add(scopeStack);
            topBar.Child = topStack;
            WpfGrid.SetRow(topBar, 1);
            mainGrid.Children.Add(topBar);

            // 3. Тело с настройками
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(14) };
            StackPanel bodyStack = new StackPanel();

            // Блок СТЕНЫ И ПРОЕМЫ
            Border wallCard = CreateSectionCard("СТЕНЫ И ПРОЁМЫ", bgCard, borderCard);
            StackPanel wallPanel = new StackPanel();

            _chkWallChains = CreateDarkCheckBox("Продольные размерные цепочки по стенам и проёмам", true);
            _chkWallGrids = CreateDarkCheckBox("Включать в цепочку пересекающие координационные оси", true);
            _chkWallThickness = CreateDarkCheckBox("Поперечный размер (толщина стены)", true);

            wallPanel.Children.Add(_chkWallChains);
            wallPanel.Children.Add(_chkWallGrids);
            wallPanel.Children.Add(_chkWallThickness);

            // Отступ и толщина
            WpfGrid numGrid = new WpfGrid { Margin = new Thickness(0, 8, 0, 4) };
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            TextBlock lblOffset = new TextBlock { Text = "Отступ линии, мм:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(lblOffset, 0);
            numGrid.Children.Add(lblOffset);

            _tbOffsetMm = new TextBox
            {
                Text = "800",
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(4),
                Margin = new Thickness(8, 0, 0, 0)
            };
            WpfGrid.SetColumn(_tbOffsetMm, 1);
            numGrid.Children.Add(_tbOffsetMm);

            TextBlock lblMinWidth = new TextBlock { Text = "Мин. толщина, мм:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            WpfGrid.SetColumn(lblMinWidth, 3);
            numGrid.Children.Add(lblMinWidth);

            _tbMinWallWidthMm = new TextBox
            {
                Text = "50",
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(4),
                Margin = new Thickness(8, 0, 0, 0)
            };
            WpfGrid.SetColumn(_tbMinWallWidthMm, 4);
            numGrid.Children.Add(_tbMinWallWidthMm);

            wallPanel.Children.Add(numGrid);

            // Исключения типов
            WpfGrid exclGrid = new WpfGrid { Margin = new Thickness(0, 8, 0, 0) };
            exclGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            exclGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblExcl = new TextBlock { Text = "Исключать типы:", Foreground = textMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            WpfGrid.SetColumn(lblExcl, 0);
            exclGrid.Children.Add(lblExcl);

            _tbExcludeTypes = new TextBox
            {
                Text = "отделка, штукатурка, утеплитель",
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(4)
            };
            WpfGrid.SetColumn(_tbExcludeTypes, 1);
            exclGrid.Children.Add(_tbExcludeTypes);

            wallPanel.Children.Add(exclGrid);

            wallCard.Child = wallPanel;
            bodyStack.Children.Add(wallCard);

            // Блок КОЛОННЫ
            Border colCard = CreateSectionCard("КОЛОННЫ", bgCard, borderCard);
            colCard.Margin = new Thickness(0, 12, 0, 0);
            StackPanel colPanel = new StackPanel();

            _chkColumns = CreateDarkCheckBox("Габаритные размеры сечения колонн (ширина / глубина)", true);
            _chkColGrids = CreateDarkCheckBox("Привязка граней колонн к ближайшим координационным осям", true);

            colPanel.Children.Add(_chkColumns);
            colPanel.Children.Add(_chkColGrids);

            colCard.Child = colPanel;
            bodyStack.Children.Add(colCard);

            scroll.Content = bodyStack;
            WpfGrid.SetRow(scroll, 2);
            mainGrid.Children.Add(scroll);

            // 4. Подвал (Кнопки)
            Border footerBorder = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 10)
            };
            WpfGrid footerGrid = new WpfGrid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel btnActionPanel = new StackPanel { Orientation = Orientation.Horizontal };
            Button btnRun = new Button
            {
                Content = "Создать размеры",
                Width = 200,
                Height = 34,
                Background = accentRed,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnRun.Click += BtnRun_Click;

            Button btnCancel = new Button
            {
                Content = "Закрыть",
                Width = 90,
                Height = 34,
                Background = new SolidColorBrush(MediaColor.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => DialogResult = false;

            btnActionPanel.Children.Add(btnRun);
            btnActionPanel.Children.Add(btnCancel);
            WpfGrid.SetColumn(btnActionPanel, 1);
            footerGrid.Children.Add(btnActionPanel);

            footerBorder.Child = footerGrid;
            WpfGrid.SetRow(footerBorder, 3);
            mainGrid.Children.Add(footerBorder);
        }

        private Border CreateSectionCard(string header, SolidColorBrush bgCard, SolidColorBrush borderCard)
        {
            Border card = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12)
            };

            return card;
        }

        private CheckBox CreateDarkCheckBox(string text, bool isChecked)
        {
            return new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            double offset = 800.0;
            double.TryParse(_tbOffsetMm.Text.Trim(), out offset);
            if (offset < 50.0) offset = 50.0;

            double minWidth = 50.0;
            double.TryParse(_tbMinWallWidthMm.Text.Trim(), out minWidth);

            Options = new PlanDimensionsOptions
            {
                SelectedDimType = _cbDimTypes.SelectedItem as DimensionType,
                OffsetMm = offset,
                DimensionOpenings = _chkWallChains.IsChecked == true,
                DimensionGrids = _chkWallGrids.IsChecked == true,
                DimensionWallThickness = _chkWallThickness.IsChecked == true,
                DimensionColumns = _chkColumns.IsChecked == true,
                ColumnGridsTie = _chkColGrids.IsChecked == true,
                MinWallThicknessMm = minWidth,
                ExcludeTypeNames = _tbExcludeTypes.Text.Trim()
            };

            DialogResult = true;
        }
    }
}
