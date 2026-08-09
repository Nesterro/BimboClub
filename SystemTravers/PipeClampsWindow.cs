using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Grid = System.Windows.Controls.Grid;
using WpfBinding = System.Windows.Data.Binding;
using DataGridLengthUnitType = System.Windows.Controls.DataGridLengthUnitType;

namespace BimboClub.PipeClamps
{
    public class PipeClampRowViewModel
    {
        public PipeDiameterInfo DiameterInfo { get; set; }
        public List<FamilySymbolItem> AvailableSymbols { get; set; }
        public FamilySymbolItem SelectedSymbolItem { get; set; }
        public string StepText { get; set; } = "1500";
        public string OffsetText { get; set; } = "300";
    }

    public class FamilySymbolItem
    {
        public FamilySymbol Symbol { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName;
    }

    public class PipeClampsWindow : Window
    {
        public bool UserApproved { get; private set; } = false;
        public List<PipeDiameterInfo> ResultSettings { get; private set; }
        public PipeClampsPlacementOptions ResultOptions { get; private set; }

        private List<PipeClampRowViewModel> _rowViewModels;

        private CheckBox _chkCopyParam;
        private TextBox _txtSourceParam;
        private TextBox _txtTargetParam;
        private CheckBox _chkRotate90;

        public PipeClampsWindow(Document doc)
        {
            Title = "Расстановка хомутов — BimboClub";
            Width = 660;
            Height = 610;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            InitData(doc);
            BuildUI();

            UiThemeHelper.ApplyDarkTheme(this);
        }

        private void InitData(Document doc)
        {
            _rowViewModels = new List<PipeClampRowViewModel>();
            if (doc == null) return;

            var diameters = PipeClampsEngine.GetVerticalPipeDiameters(doc);
            var symbols = PipeClampsEngine.GetClampSymbols(doc);

            var symbolItems = new List<FamilySymbolItem>
            {
                new FamilySymbolItem { Symbol = null, DisplayName = "-- Не расставлять --" }
            };

            foreach (var sym in symbols)
            {
                symbolItems.Add(new FamilySymbolItem
                {
                    Symbol = sym,
                    DisplayName = $"{sym.FamilyName}: {sym.Name}"
                });
            }

            foreach (var info in diameters)
            {
                // Сопоставление по диаметру в наименовании типа хомута
                var autoMatched = symbolItems.FirstOrDefault(s => s.Symbol != null && s.DisplayName.Contains(((int)info.DiameterMm).ToString()));

                _rowViewModels.Add(new PipeClampRowViewModel
                {
                    DiameterInfo = info,
                    AvailableSymbols = symbolItems,
                    SelectedSymbolItem = autoMatched ?? symbolItems[0],
                    StepText = "1500",
                    OffsetText = "300"
                });
            }
        }

        private void BuildUI()
        {
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });

            // Шапка BimboClub
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(255, 46, 91),
                    System.Windows.Media.Color.FromRgb(179, 14, 45),
                    45.0
                )
            };

            TextBlock headerTitle = new TextBlock
            {
                Text = "РАССТАНОВКА ХОМУТОВ ПО СТОЯКАМ",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerTitle;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // Таблица диаметров
            if (_rowViewModels.Count == 0)
            {
                TextBlock emptyText = new TextBlock
                {
                    Text = "В проекте не найдено вертикальных трубопроводов.",
                    Foreground = System.Windows.Media.Brushes.LightGray,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(emptyText, 1);
                mainGrid.Children.Add(emptyText);
            }
            else
            {
                DataGrid grid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    ItemsSource = _rowViewModels,
                    Margin = new Thickness(15, 10, 15, 5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36)),
                    RowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 37, 45)),
                    AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 43, 52)),
                    Foreground = System.Windows.Media.Brushes.White,
                    GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                    HorizontalGridLinesBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 55, 65))
                };

                // Колонка 1: Диаметр
                DataGridTextColumn colDiam = new DataGridTextColumn
                {
                    Header = "Диаметр трубы",
                    Binding = new WpfBinding("DiameterInfo.DisplayName"),
                    IsReadOnly = true,
                    Width = new DataGridLength(140)
                };
                grid.Columns.Add(colDiam);

                // Колонка 2: Типоразмер хомута (ComboBox)
                DataGridComboBoxColumn colSymbol = new DataGridComboBoxColumn
                {
                    Header = "Типоразмер хомута (Оборудование)",
                    SelectedValueBinding = new WpfBinding("SelectedSymbolItem"),
                    DisplayMemberPath = "DisplayName",
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                };
                colSymbol.ItemsSource = _rowViewModels[0].AvailableSymbols;
                grid.Columns.Add(colSymbol);

                // Колонка 3: Шаг (мм)
                DataGridTextColumn colStep = new DataGridTextColumn
                {
                    Header = "Шаг (мм)",
                    Binding = new WpfBinding("StepText"),
                    Width = new DataGridLength(80)
                };
                grid.Columns.Add(colStep);

                // Колонка 4: Отступ (мм)
                DataGridTextColumn colOffset = new DataGridTextColumn
                {
                    Header = "Отступ (мм)",
                    Binding = new WpfBinding("OffsetText"),
                    Width = new DataGridLength(85)
                };
                grid.Columns.Add(colOffset);

                Grid.SetRow(grid, 1);
                mainGrid.Children.Add(grid);
            }

            // Блок опций переноса параметров и ориентации
            Border paramBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 37, 45)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 72)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(15, 5, 15, 5),
                Padding = new Thickness(10)
            };

            StackPanel paramPanel = new StackPanel();

            _chkCopyParam = new CheckBox
            {
                Content = "Переносить параметр стояка в хомут",
                IsChecked = true,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            };
            paramPanel.Children.Add(_chkCopyParam);

            Grid fieldsGrid = new Grid();
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel p1 = new StackPanel();
            p1.Children.Add(new TextBlock
            {
                Text = "Параметр из трубы (источник):",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 190, 200)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            });
            _txtSourceParam = new TextBox
            {
                Text = "ADSK_Номер стояка",
                Height = 26,
                Padding = new Thickness(4, 2, 4, 2)
            };
            p1.Children.Add(_txtSourceParam);
            Grid.SetColumn(p1, 0);
            fieldsGrid.Children.Add(p1);

            StackPanel p2 = new StackPanel();
            p2.Children.Add(new TextBlock
            {
                Text = "Параметр в хомуте (приемник):",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 190, 200)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3)
            });
            _txtTargetParam = new TextBox
            {
                Text = "ADSK_Номер стояка",
                Height = 26,
                Padding = new Thickness(4, 2, 4, 2)
            };
            p2.Children.Add(_txtTargetParam);
            Grid.SetColumn(p2, 2);
            fieldsGrid.Children.Add(p2);

            paramPanel.Children.Add(fieldsGrid);

            _chkRotate90 = new CheckBox
            {
                Content = "Повернуть хомуты на 90° относительно оси стояка",
                IsChecked = false,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            };
            paramPanel.Children.Add(_chkRotate90);

            paramBorder.Child = paramPanel;

            Grid.SetRow(paramBorder, 2);
            mainGrid.Children.Add(paramBorder);

            // Подвал с кнопкой
            Border footerBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 60)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            Button btnStart = new Button
            {
                Content = "Расставить хомуты",
                Width = 200,
                Height = 36,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45)),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = _rowViewModels.Count > 0
            };
            btnStart.Click += BtnStart_Click;
            footerBorder.Child = btnStart;

            Grid.SetRow(footerBorder, 3);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            ResultSettings = new List<PipeDiameterInfo>();

            foreach (var vm in _rowViewModels)
            {
                if (vm.SelectedSymbolItem != null && vm.SelectedSymbolItem.Symbol != null)
                {
                    double.TryParse(vm.StepText, out double step);
                    double.TryParse(vm.OffsetText, out double offset);

                    if (step <= 0) step = 1500;
                    if (offset < 0) offset = 300;

                    var info = vm.DiameterInfo;
                    info.SelectedClampSymbol = vm.SelectedSymbolItem.Symbol;
                    info.StepMm = step;
                    info.OffsetMm = offset;

                    ResultSettings.Add(info);
                }
            }

            if (ResultSettings.Count == 0)
            {
                System.Windows.MessageBox.Show("Пожалуйста, выберите хотя бы один типоразмер хомута для расстановки.", "BimboClub — Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultOptions = new PipeClampsPlacementOptions
            {
                CopyRiserParameter = _chkCopyParam.IsChecked == true,
                SourceParamName = _txtSourceParam.Text?.Trim(),
                TargetParamName = _txtTargetParam.Text?.Trim(),
                Rotate90Degrees = _chkRotate90.IsChecked == true
            };

            UserApproved = true;
            DialogResult = true;
            Close();
        }
    }
}
