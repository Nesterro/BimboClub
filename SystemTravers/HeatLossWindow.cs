using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class RoomHeatLossItem : INotifyPropertyChanged
    {
        private double _transmissionLoss;
        private double _ventilationLoss;
        private double _totalLoss;

        public string Number { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public double Volume { get; set; }
        public object RawRoom { get; set; } // Autodesk.Revit.DB.Architecture.Room or SpatialElement

        public double TransmissionLoss
        {
            get => _transmissionLoss;
            set { _transmissionLoss = value; OnPropertyChanged(nameof(TransmissionLoss)); }
        }

        public double VentilationLoss
        {
            get => _ventilationLoss;
            set { _ventilationLoss = value; OnPropertyChanged(nameof(VentilationLoss)); }
        }

        public double TotalLoss
        {
            get => _totalLoss;
            set { _totalLoss = value; OnPropertyChanged(nameof(TotalLoss)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class HeatLossWindow : Window
    {
        private TextBox _txtTempIndoor;
        private TextBox _txtTempOutdoor;
        private TextBox _txtUWallExt;
        private TextBox _txtUWindow;
        private TextBox _txtUDoor;
        private TextBox _txtURoof;
        private TextBox _txtUFloor;
        private TextBox _txtAirChanges;
        private ListView _lvRooms;
        private ComboBox _cbParameters;

        private Button _btnCalculate;
        private Button _btnWriteToRevit;
        private Button _btnExportExcel;

        public double TempIndoor => double.TryParse(_txtTempIndoor.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 20.0;
        public double TempOutdoor => double.TryParse(_txtTempOutdoor.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : -25.0;
        public double UWallExt => double.TryParse(_txtUWallExt.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0.35;
        public double UWindow => double.TryParse(_txtUWindow.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 1.5;
        public double UDoor => double.TryParse(_txtUDoor.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 2.0;
        public double URoof => double.TryParse(_txtURoof.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0.2;
        public double UFloor => double.TryParse(_txtUFloor.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0.3;
        public double AirChanges => double.TryParse(_txtAirChanges.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 1.0;

        public string SelectedParameter => _cbParameters.SelectedItem?.ToString();

        public List<RoomHeatLossItem> Rooms { get; private set; }

        public event Action CalculateRequested;
        public event Action WriteToRevitRequested;
        public event Action ExportExcelRequested;

        public HeatLossWindow(List<RoomHeatLossItem> rooms, List<string> availableParameters)
        {
            Rooms = rooms;
            InitializeComponent(availableParameters);
            _lvRooms.ItemsSource = Rooms;
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
        }

        private void ApplyFlatButtonStyles(Button btn, SolidColorBrush normalBrush, SolidColorBrush hoverBrush, Style roundedStyle)
        {
            btn.Style = roundedStyle;
            btn.Background = normalBrush;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverBrush; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalBrush; };
        }

        private void InitializeComponent(List<string> availableParameters)
        {
            this.Title = "Расчет теплопотерь - BimboClub Tools";
            this.Width = 850;
            this.Height = 620;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            this.FontFamily = new FontFamily("Segoe UI");
            this.FontSize = 13;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush inputBg = new SolidColorBrush(Color.FromRgb(45, 45, 53)); // #2D2D35
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B
            SolidColorBrush cancelNormal = new SolidColorBrush(Color.FromRgb(58, 58, 66));
            SolidColorBrush cancelHover = new SolidColorBrush(Color.FromRgb(78, 78, 88));

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, inputBg));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(5, 2, 5, 2)));
            textBoxStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, inputBg));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, borderBrush));

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(165) });   // Настройки U-values
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Результаты расчетов
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });    // Кнопки записи/экспорта
            this.Content = mainGrid;

            // 1. Заголовок
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(18, 18, 20), Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "РАСЧЕТ ТЕПЛОПОТЕРЬ ПОМЕЩЕНИЙ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Настройки U-values & Температур
            Border settingsBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(15, 10, 15, 5),
                Padding = new Thickness(12)
            };
            Grid.SetRow(settingsBorder, 1);
            mainGrid.Children.Add(settingsBorder);

            Grid settingsGrid = new Grid();
            settingsBorder.Child = settingsGrid;

            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            TextBlock lblSecTitle = new TextBlock
            {
                Text = "ПАРАМЕТРЫ РАСЧЕТА И КОЭФФИЦИЕНТЫ ТЕПЛОПЕРЕДАЧИ (U-values):",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            Grid.SetRow(lblSecTitle, 0); Grid.SetColumnSpan(lblSecTitle, 8);
            settingsGrid.Children.Add(lblSecTitle);

            // Row 1
            AddLabel(settingsGrid, "Т внутр. default (°C):", 1, 0);
            _txtTempIndoor = new TextBox { Style = textBoxStyle, Text = "20" };
            Grid.SetRow(_txtTempIndoor, 1); Grid.SetColumn(_txtTempIndoor, 1);
            settingsGrid.Children.Add(_txtTempIndoor);

            AddLabel(settingsGrid, "U Наружные стены:", 1, 3);
            _txtUWallExt = new TextBox { Style = textBoxStyle, Text = "0.35" };
            Grid.SetRow(_txtUWallExt, 1); Grid.SetColumn(_txtUWallExt, 4);
            settingsGrid.Children.Add(_txtUWallExt);

            AddLabel(settingsGrid, "U Перекрытия/Кровля:", 1, 6);
            _txtURoof = new TextBox { Style = textBoxStyle, Text = "0.20" };
            Grid.SetRow(_txtURoof, 1); Grid.SetColumn(_txtURoof, 7);
            settingsGrid.Children.Add(_txtURoof);

            // Row 2
            AddLabel(settingsGrid, "Т наруж. расч. (°C):", 2, 0);
            _txtTempOutdoor = new TextBox { Style = textBoxStyle, Text = "-25" };
            Grid.SetRow(_txtTempOutdoor, 2); Grid.SetColumn(_txtTempOutdoor, 1);
            settingsGrid.Children.Add(_txtTempOutdoor);

            AddLabel(settingsGrid, "U Окна:", 2, 3);
            _txtUWindow = new TextBox { Style = textBoxStyle, Text = "1.50" };
            Grid.SetRow(_txtUWindow, 2); Grid.SetColumn(_txtUWindow, 4);
            settingsGrid.Children.Add(_txtUWindow);

            AddLabel(settingsGrid, "U Полы по грунту:", 2, 6);
            _txtUFloor = new TextBox { Style = textBoxStyle, Text = "0.30" };
            Grid.SetRow(_txtUFloor, 2); Grid.SetColumn(_txtUFloor, 7);
            settingsGrid.Children.Add(_txtUFloor);

            // Row 3
            AddLabel(settingsGrid, "Кратность возд. (1/ч):", 3, 0);
            _txtAirChanges = new TextBox { Style = textBoxStyle, Text = "1.0" };
            Grid.SetRow(_txtAirChanges, 3); Grid.SetColumn(_txtAirChanges, 1);
            settingsGrid.Children.Add(_txtAirChanges);

            AddLabel(settingsGrid, "U Наружные двери:", 3, 3);
            _txtUDoor = new TextBox { Style = textBoxStyle, Text = "2.00" };
            Grid.SetRow(_txtUDoor, 3); Grid.SetColumn(_txtUDoor, 4);
            settingsGrid.Children.Add(_txtUDoor);

            _btnCalculate = new Button
            {
                Content = "Рассчитать",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 26,
                Cursor = Cursors.Hand
            };
            _btnCalculate.Click += (s, e) => CalculateRequested?.Invoke();
            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnCalculate, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetRow(_btnCalculate, 3); Grid.SetColumn(_btnCalculate, 6); Grid.SetColumnSpan(_btnCalculate, 2);
            settingsGrid.Children.Add(_btnCalculate);

            // 3. Таблица результатов
            Border tableBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(15, 5, 15, 5),
                Padding = new Thickness(5)
            };
            Grid.SetRow(tableBorder, 2);
            mainGrid.Children.Add(tableBorder);

            _lvRooms = new ListView
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White
            };
            tableBorder.Child = _lvRooms;

            GridView gv = new GridView();
            gv.Columns.Add(new GridViewColumn { Header = "Номер", DisplayMemberBinding = new Binding("Number"), Width = 60 });
            gv.Columns.Add(new GridViewColumn { Header = "Имя помещения", DisplayMemberBinding = new Binding("Name"), Width = 180 });
            gv.Columns.Add(new GridViewColumn { Header = "Площадь (м²)", DisplayMemberBinding = new Binding("Area") { StringFormat = "{0:N2}" }, Width = 90 });
            gv.Columns.Add(new GridViewColumn { Header = "Объем (м³)", DisplayMemberBinding = new Binding("Volume") { StringFormat = "{0:N2}" }, Width = 90 });
            gv.Columns.Add(new GridViewColumn { Header = "Трансмиссия (Вт)", DisplayMemberBinding = new Binding("TransmissionLoss") { StringFormat = "{0:N0}" }, Width = 120 });
            gv.Columns.Add(new GridViewColumn { Header = "Вентиляция (Вт)", DisplayMemberBinding = new Binding("VentilationLoss") { StringFormat = "{0:N0}" }, Width = 120 });
            gv.Columns.Add(new GridViewColumn { Header = "Теплопотери всего (Вт)", DisplayMemberBinding = new Binding("TotalLoss") { StringFormat = "{0:N0}" }, Width = 145 });
            _lvRooms.View = gv;

            // 4. Подвал
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // ComboBox параметров
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Записать в Revit
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Экспорт в Excel
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });  // Закрыть
            Grid.SetRow(bottomGrid, 3);
            mainGrid.Children.Add(bottomGrid);

            _cbParameters = new ComboBox { Style = comboBoxStyle, Height = 35 };
            UiThemeHelper.StyleComboBoxDark(_cbParameters, inputBg, borderBrush);
            foreach (var pName in availableParameters)
            {
                _cbParameters.Items.Add(pName);
            }
            if (_cbParameters.Items.Count > 0) _cbParameters.SelectedIndex = 0;
            Grid.SetColumn(_cbParameters, 0);
            bottomGrid.Children.Add(_cbParameters);

            _btnWriteToRevit = new Button
            {
                Content = "Записать в Revit",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = Cursors.Hand
            };
            _btnWriteToRevit.Click += (s, e) => WriteToRevitRequested?.Invoke();
            ApplyButtonStyles(_btnWriteToRevit, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnWriteToRevit, 2);
            bottomGrid.Children.Add(_btnWriteToRevit);

            _btnExportExcel = new Button
            {
                Content = "Экспорт в Excel",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = Cursors.Hand
            };
            _btnExportExcel.Click += (s, e) => ExportExcelRequested?.Invoke();
            ApplyFlatButtonStyles(_btnExportExcel, cancelNormal, cancelHover, roundedButtonStyle);
            Grid.SetColumn(_btnExportExcel, 4);
            bottomGrid.Children.Add(_btnExportExcel);

            Button btnClose = new Button
            {
                Content = "Закрыть",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => Close();
            ApplyFlatButtonStyles(btnClose, cancelNormal, cancelHover, roundedButtonStyle);
            Grid.SetColumn(btnClose, 6);
            bottomGrid.Children.Add(btnClose);
        }

        private static void AddLabel(Grid grid, string text, int row, int col)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }
    }
}
