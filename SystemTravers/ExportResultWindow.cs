using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class ExportResultWindow : Window
    {
        public bool OpenFolderRequested { get; private set; } = false;

        public ExportResultWindow(string statusText, string folderPath)
        {
            Title = "Экспорт чертежей - BimboClub Tools";
            Width = 450;
            Height = 240;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            // Styles
            Style roundedButtonStyle = GetRoundedButtonStyle(4);
            LinearGradientBrush redBtnGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush redBtnHover = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });    // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Footer
            Content = grid;

            // Header
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(18, 18, 20), Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15, 0, 15, 0)
            };
            Grid.SetRow(headerBorder, 0);
            grid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "ЭКСПОРТ ЧЕРТЕЖЕЙ ЗАВЕРШЕН!",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // Content
            StackPanel contentStack = new StackPanel { Margin = new Thickness(15, 12, 15, 10) };
            Grid.SetRow(contentStack, 1);
            grid.Children.Add(contentStack);

            TextBlock txtStatus = new TextBlock
            {
                Text = statusText,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            contentStack.Children.Add(txtStatus);

            TextBlock txtPath = new TextBlock
            {
                Text = $"Сохранено в: {folderPath}",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 170)),
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            };
            contentStack.Children.Add(txtPath);

            // Footer / Action Buttons
            Grid footerGrid = new Grid { Margin = new Thickness(15, 5, 15, 10) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            Grid.SetRow(footerGrid, 2);
            grid.Children.Add(footerGrid);

            Button btnOpen = new Button
            {
                Content = "Открыть папку с файлами",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 32,
                Cursor = Cursors.Hand
            };
            btnOpen.Style = roundedButtonStyle;
            btnOpen.Background = redBtnGrad;
            btnOpen.MouseEnter += (s, e) => btnOpen.Background = redBtnHover;
            btnOpen.MouseLeave += (s, e) => btnOpen.Background = redBtnGrad;
            btnOpen.Click += (s, e) =>
            {
                OpenFolderRequested = true;
                DialogResult = true;
                Close();
            };
            Grid.SetColumn(btnOpen, 1);
            footerGrid.Children.Add(btnOpen);

            Button btnClose = new Button
            {
                Content = "Закрыть",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 32,
                Cursor = Cursors.Hand
            };
            btnClose.Style = roundedButtonStyle;
            SolidColorBrush normalBrush = new SolidColorBrush(Color.FromRgb(58, 58, 66));
            SolidColorBrush hoverBrush = new SolidColorBrush(Color.FromRgb(78, 78, 88));
            btnClose.Background = normalBrush;
            btnClose.MouseEnter += (s, e) => btnClose.Background = hoverBrush;
            btnClose.MouseLeave += (s, e) => btnClose.Background = normalBrush;
            btnClose.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };
            Grid.SetColumn(btnClose, 3);
            footerGrid.Children.Add(btnClose);
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
    }
}
