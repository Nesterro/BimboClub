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

        private Document _doc;
        private List<PipeClampRowViewModel> _rowViewModels;

        private CheckBox _chkActiveViewOnly;

        private CheckBox _chkCopyParam1;
        private TextBox _txtSourceParam1;
        private TextBox _txtTargetParam1;

        private CheckBox _chkCopyParam2;
        private TextBox _txtSourceParam2;
        private TextBox _txtTargetParam2;

        private TextBox _txtRotateX;
        private TextBox _txtRotateY;
        private TextBox _txtRotateZ;

        public PipeClampsWindow(Document doc)
        {
            _doc = doc;
            Title = "Расстановка хомутов — BimboClub";
            Width = 720;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            InitData(doc, false);
            BuildUI();

            UiThemeHelper.ApplyDarkTheme(this);
        }

        private void InitData(Document doc, bool activeViewOnly)
        {
            _rowViewModels = new List<PipeClampRowViewModel>();
            if (doc == null) return;

            var diameters = PipeClampsEngine.GetVerticalPipeDiameters(doc, activeViewOnly);
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });

            // 1. Шапка BimboClub (яокрая контрастная)
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

            // 2. Выбор области расстановки (Весь проект vs Текущий вид)
            Border scopeBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 35)),
                Padding = new Thickness(15, 8, 15, 8)
            };

            _chkActiveViewOnly = new CheckBox
            {
                Content = "Расставлять хомуты только на текущем виде",
                IsChecked = false,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            _chkActiveViewOnly.Checked += ChkActiveViewOnly_Changed;
            _chkActiveViewOnly.Unchecked += ChkActiveViewOnly_Changed;

            scopeBorder.Child = _chkActiveViewOnly;
            Grid.SetRow(scopeBorder, 1);
            mainGrid.Children.Add(scopeBorder);

            // 3. Таблица диаметров с высокими контрастными цветами
            DataGrid grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                ItemsSource = _rowViewModels,
                Margin = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 30)),
                RowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 34, 42)),
                AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 52)),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 75))
            };

            // Колонка 1: Диаметр
            DataGridTextColumn colDiam = new DataGridTextColumn
            {
                Header = "Диаметр трубы",
                Binding = new WpfBinding("DiameterInfo.DisplayName"),
                IsReadOnly = true,
                Width = new DataGridLength(150)
            };
            grid.Columns.Add(colDiam);

            // Колонка 2: Типоразмер хомута
            DataGridComboBoxColumn colSymbol = new DataGridComboBoxColumn
            {
                Header = "Типоразмер хомута (Оборудование)",
                SelectedValueBinding = new WpfBinding("SelectedSymbolItem"),
                DisplayMemberPath = "DisplayName",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            if (_rowViewModels.Count > 0)
            {
                colSymbol.ItemsSource = _rowViewModels[0].AvailableSymbols;
            }
            grid.Columns.Add(colSymbol);

            // Колонка 3: Шаг (мм)
            DataGridTextColumn colStep = new DataGridTextColumn
            {
                Header = "Шаг (мм)",
                Binding = new WpfBinding("StepText"),
                Width = new DataGridLength(85)
            };
            grid.Columns.Add(colStep);

            // Колонка 4: Отступ (мм)
            DataGridTextColumn colOffset = new DataGridTextColumn
            {
                Header = "Отступ (мм)",
                Binding = new WpfBinding("OffsetText"),
                Width = new DataGridLength(90)
            };
            grid.Columns.Add(colOffset);

            Grid.SetRow(grid, 2);
            mainGrid.Children.Add(grid);

            // 4. Блок настроек параметров и 3D поворота
            Border paramBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 34, 42)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(15, 5, 15, 5),
                Padding = new Thickness(12)
            };

            StackPanel paramPanel = new StackPanel();

            // Секция параметров перезаписи
            TextBlock secParamTitle = new TextBlock
            {
                Text = "Перенос параметров из трубы стояка в хомут:",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            paramPanel.Children.Add(secParamTitle);

            // Параметр 1 (ADSK_Номер стояка)
            _chkCopyParam1 = new CheckBox
            {
                Content = "Параметр 1 (например, ADSK_Номер стояка):",
                IsChecked = true,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            };
            paramPanel.Children.Add(_chkCopyParam1);

            Grid fGrid1 = new Grid();
            fGrid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fGrid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            fGrid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _txtSourceParam1 = CreateHighContrastTextBox("ADSK_Номер стояка");
            _txtTargetParam1 = CreateHighContrastTextBox("ADSK_Номер стояка");
            Grid.SetColumn(_txtSourceParam1, 0);
            Grid.SetColumn(_txtTargetParam1, 2);
            fGrid1.Children.Add(_txtSourceParam1);
            fGrid1.Children.Add(_txtTargetParam1);
            paramPanel.Children.Add(fGrid1);

            // Параметр 2 (ADSK_Секция)
            _chkCopyParam2 = new CheckBox
            {
                Content = "Параметр 2 (например, ADSK_Секция):",
                IsChecked = true,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 4)
            };
            paramPanel.Children.Add(_chkCopyParam2);

            Grid fGrid2 = new Grid();
            fGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            fGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _txtSourceParam2 = CreateHighContrastTextBox("ADSK_Секция");
            _txtTargetParam2 = CreateHighContrastTextBox("ADSK_Секция");
            Grid.SetColumn(_txtSourceParam2, 0);
            Grid.SetColumn(_txtTargetParam2, 2);
            fGrid2.Children.Add(_txtSourceParam2);
            fGrid2.Children.Add(_txtTargetParam2);
            paramPanel.Children.Add(fGrid2);

            // Секция 3D Поворота
            TextBlock rotTitle = new TextBlock
            {
                Text = "Ориентация / Поворот хомута в 3D плоскостях (в градусах):",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 12, 0, 6)
            };
            paramPanel.Children.Add(rotTitle);

            Grid rotGrid = new Grid();
            rotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Ось X
            StackPanel rx = new StackPanel();
            rx.Children.Add(new TextBlock { Text = "Ось X (наклон 1):", Foreground = System.Windows.Media.Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });
            _txtRotateX = CreateHighContrastTextBox("0");
            rx.Children.Add(_txtRotateX);
            Grid.SetColumn(rx, 0);
            rotGrid.Children.Add(rx);

            // Ось Y
            StackPanel ry = new StackPanel();
            ry.Children.Add(new TextBlock { Text = "Ось Y (наклон 2):", Foreground = System.Windows.Media.Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });
            _txtRotateY = CreateHighContrastTextBox("0");
            ry.Children.Add(_txtRotateY);
            Grid.SetColumn(ry, 2);
            rotGrid.Children.Add(ry);

            // Ось Z
            StackPanel rz = new StackPanel();
            rz.Children.Add(new TextBlock { Text = "Ось Z (вокруг стояка):", Foreground = System.Windows.Media.Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });
            _txtRotateZ = CreateHighContrastTextBox("0");
            rz.Children.Add(_txtRotateZ);
            Grid.SetColumn(rz, 4);
            rotGrid.Children.Add(rz);

            paramPanel.Children.Add(rotGrid);

            paramBorder.Child = paramPanel;
            Grid.SetRow(paramBorder, 3);
            mainGrid.Children.Add(paramBorder);

            // 5. Подвал с кнопкой действия
            Border footerBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 30)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 60)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            Button btnStart = new Button
            {
                Content = "Расставить хомуты",
                Width = 220,
                Height = 38,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45)),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnStart.Click += BtnStart_Click;
            footerBorder.Child = btnStart;

            Grid.SetRow(footerBorder, 4);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

        private TextBox CreateHighContrastTextBox(string defaultText)
        {
            return new TextBox
            {
                Text = defaultText,
                Height = 26,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 18, 24)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 120)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 2, 5, 2),
                FontWeight = FontWeights.Medium,
                FontSize = 12
            };
        }

        private void ChkActiveViewOnly_Changed(object sender, RoutedEventArgs e)
        {
            bool activeViewOnly = _chkActiveViewOnly.IsChecked == true;
            InitData(_doc, activeViewOnly);
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

            double.TryParse(_txtRotateX.Text, out double rx);
            double.TryParse(_txtRotateY.Text, out double ry);
            double.TryParse(_txtRotateZ.Text, out double rz);

            ResultOptions = new PipeClampsPlacementOptions
            {
                ActiveViewOnly = _chkActiveViewOnly.IsChecked == true,

                CopyParam1 = _chkCopyParam1.IsChecked == true,
                SourceParam1 = _txtSourceParam1.Text?.Trim(),
                TargetParam1 = _txtTargetParam1.Text?.Trim(),

                CopyParam2 = _chkCopyParam2.IsChecked == true,
                SourceParam2 = _txtSourceParam2.Text?.Trim(),
                TargetParam2 = _txtTargetParam2.Text?.Trim(),

                RotateXDeg = rx,
                RotateYDeg = ry,
                RotateZDeg = rz
            };

            UserApproved = true;
            DialogResult = true;
            Close();
        }
    }
}
