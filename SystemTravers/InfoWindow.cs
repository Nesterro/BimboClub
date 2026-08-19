using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace BimboClub
{
    public class InfoWindow : Window
    {
        public InfoWindow(UIApplication uiApp)
        {
            Title = "О плагине BimboClub Tools";
            Width = 460;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)); // Dark #1E1E2E

            string currentVer = InfoCommand.GetCurrentVersion();
            string revitVer = uiApp.Application.VersionNumber;

            // Main Layout
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Footer

            // Header Banner
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 58)),
                Padding = new Thickness(20, 15, 20, 15)
            };
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            var headerIcon = new TextBlock
            {
                Text = "⚫",
                FontSize = 24,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var headerTextStack = new StackPanel();
            var titleBlock = new TextBlock
            {
                Text = "BimboClub Tools",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0))
            };
            var subtitleBlock = new TextBlock
            {
                Text = $"Набор профессиональных плагинов MEP для Revit {revitVer}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 200))
            };
            headerTextStack.Children.Add(titleBlock);
            headerTextStack.Children.Add(subtitleBlock);
            headerStack.Children.Add(headerIcon);
            headerStack.Children.Add(headerTextStack);
            headerBorder.Child = headerStack;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // Content Body
            var contentStack = new StackPanel { Margin = new Thickness(20, 15, 20, 15) };

            // Version Card
            var verCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 63)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var verGrid = new Grid();
            verGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            verGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var verLabelStack = new StackPanel();
            verLabelStack.Children.Add(new TextBlock
            {
                Text = "Установленная версия плагина:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 220))
            });
            verLabelStack.Children.Add(new TextBlock
            {
                Text = $"Версия v{currentVer}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 0)
            });

            var statusBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadge.Child = new TextBlock
            {
                Text = "Активен",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            Grid.SetColumn(statusBadge, 1);

            verGrid.Children.Add(verLabelStack);
            verGrid.Children.Add(statusBadge);
            verCard.Child = verGrid;
            contentStack.Children.Add(verCard);

            // Additional Info List
            var infoText = new TextBlock
            {
                Text = $"• Версия Revit: {revitVer}\n" +
                       $"• Разработчик: BimboClub Team\n" +
                       $"• Поддержка версий Revit: 2021 – 2026\n" +
                       $"• Репозиторий: github.com/Nesterro/BimboClub",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 225)),
                LineHeight = 20
            };
            contentStack.Children.Add(infoText);

            Grid.SetRow(contentStack, 1);
            mainGrid.Children.Add(contentStack);

            // Footer / Actions
            var footerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 50)),
                Padding = new Thickness(20, 10, 20, 10)
            };
            var footerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnManager = new Button
            {
                Content = "⚙️ Менеджер обновлений",
                Width = 190,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 30)),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnManager.Click += (s, e) => LaunchManager();

            var btnClose = new Button
            {
                Content = "Закрыть",
                Width = 90,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 80)),
                Foreground = Brushes.White,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s, e) => Close();

            footerStack.Children.Add(btnManager);
            footerStack.Children.Add(btnClose);
            footerBorder.Child = footerStack;

            Grid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

        private void LaunchManager()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string managerPath = Path.Combine(appData, "BimboClubManager", "BimboClubManager.exe");

                if (!File.Exists(managerPath))
                {
                    // Fallback to Yandex Disk or default path
                    managerPath = @"D:\Yandex.Disk\Revit\Plugins\BimboClubManager\BimboClubManager.exe";
                }

                if (File.Exists(managerPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = managerPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show(
                        "BimboClubManager не найден по стандартному пути. Вы можете скачать последнюю версию с GitHub Releases.",
                        "Менеджер не найден",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска менеджера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
