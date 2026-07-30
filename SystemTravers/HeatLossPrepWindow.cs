using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using RevitPhase = Autodesk.Revit.DB.Phase;
using SpatialElementBoundaryLocation = Autodesk.Revit.DB.SpatialElementBoundaryLocation;

namespace BimboClub.HeatLoss
{
    public class HeatLossPrepWindow : Window
    {
        public bool UserApproved { get; private set; } = false;
        public HeatLossAnalysisOptions SelectedOptions { get; private set; }

        private CheckBox _chkDeleteOldCubes;
        private CheckBox _chkCreateSpaces;
        private ComboBox _cmbPhases;
        private ComboBox _cmbBoundaryLocation;
        private ProgressBar _progressBar;
        private TextBlock _lblStatus;
        private Button _btnStart;

        private List<RevitPhase> _phases;

        public HeatLossPrepWindow(Autodesk.Revit.DB.Document doc)
        {
            Title = "Подготовка данных для расчета теплопотерь — BimboClub";
            Width = 480;
            Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            InitPhases(doc);
            BuildUI();

            UiThemeHelper.ApplyDarkTheme(this);
        }

        private void InitPhases(Autodesk.Revit.DB.Document doc)
        {
            _phases = new List<RevitPhase>();
            if (doc != null)
            {
                foreach (RevitPhase ph in doc.Phases)
                {
                    _phases.Add(ph);
                }
            }
        }

        private void BuildUI()
        {
            System.Windows.Controls.Grid mainGrid = new System.Windows.Controls.Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });

            // 1. Шапка BimboClub
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
                Text = "ПОДГОТОВКА ДАННЫХ ТЕПЛОПОТЕРЬ",
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerTitle;
            System.Windows.Controls.Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. Тело настроек
            StackPanel bodyPanel = new StackPanel { Margin = new Thickness(20, 15, 20, 15) };

            // Опция 1: Удалять старые кубики
            _chkDeleteOldCubes = new CheckBox
            {
                Content = "Удалять существующие кубики перед расчетом",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 12)
            };
            bodyPanel.Children.Add(_chkDeleteOldCubes);

            // Опция 2: Создавать Пространства
            _chkCreateSpaces = new CheckBox
            {
                Content = "Автоматически создавать Пространства (Spaces) по Помещениям",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 16)
            };
            bodyPanel.Children.Add(_chkCreateSpaces);

            // Выбор фазы
            bodyPanel.Children.Add(new TextBlock
            {
                Text = "Фаза проекта:",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 210)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            });

            _cmbPhases = new ComboBox
            {
                DisplayMemberPath = "Name",
                ItemsSource = _phases,
                Margin = new Thickness(0, 0, 0, 16),
                Height = 30
            };
            if (_phases.Count > 0)
            {
                _cmbPhases.SelectedIndex = _phases.Count - 1; // Последняя фаза по умолчанию
            }
            bodyPanel.Children.Add(_cmbPhases);

            // Выбор детализации границ
            bodyPanel.Children.Add(new TextBlock
            {
                Text = "Детализация границ элементов:",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 210)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            });

            _cmbBoundaryLocation = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 16),
                Height = 30
            };
            _cmbBoundaryLocation.Items.Add("Чистая отделка (Finish)");
            _cmbBoundaryLocation.Items.Add("Сердцевина стены (Core)");
            _cmbBoundaryLocation.SelectedIndex = 0;
            bodyPanel.Children.Add(_cmbBoundaryLocation);

            // Прогресс бар
            _progressBar = new ProgressBar
            {
                Height = 10,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visibility = System.Windows.Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 4)
            };
            bodyPanel.Children.Add(_progressBar);

            _lblStatus = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 190)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            bodyPanel.Children.Add(_lblStatus);

            System.Windows.Controls.Grid.SetRow(bodyPanel, 1);
            mainGrid.Children.Add(bodyPanel);

            // 3. Футер с кнопкой
            Border footerBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 60)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            _btnStart = new Button
            {
                Content = "Запустить расчет",
                Width = 180,
                Height = 36,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnStart.Click += BtnStart_Click;
            footerBorder.Child = _btnStart;

            System.Windows.Controls.Grid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            SelectedOptions = new HeatLossAnalysisOptions
            {
                DeleteOldCubes = _chkDeleteOldCubes.IsChecked == true,
                CreateSpaces = _chkCreateSpaces.IsChecked == true,
                SelectedPhase = _cmbPhases.SelectedItem as RevitPhase,
                BoundaryLocation = _cmbBoundaryLocation.SelectedIndex == 1
                    ? SpatialElementBoundaryLocation.CoreBoundary
                    : SpatialElementBoundaryLocation.Finish
            };

            UserApproved = true;
            DialogResult = true;
            Close();
        }

        public void UpdateProgress(int percent, string message)
        {
            Dispatcher.Invoke(() =>
            {
                _progressBar.Visibility = System.Windows.Visibility.Visible;
                _progressBar.Value = percent;
                _lblStatus.Text = message;
            });
        }
    }
}
