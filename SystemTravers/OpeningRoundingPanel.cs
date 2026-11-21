using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BimboClub
{
    public sealed class OpeningRoundingPanel : UserControl
    {
        private readonly TextBox _size = new TextBox { Text = "10", MinWidth = 70, Margin = new Thickness(8, 2, 0, 2) };
        private readonly TextBox _bottom = new TextBox { Text = "10", MinWidth = 70, Margin = new Thickness(8, 2, 0, 2) };

        public OpeningRoundingPanel()
        {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Add(grid, new TextBlock { Text = "Округление размеров вверх (шаг, мм)", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }, 0, 0);
            Add(grid, _size, 0, 1);
            Add(grid, new TextBlock { Text = "Округление отметки низа вниз (шаг, мм)", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }, 1, 0);
            Add(grid, _bottom, 1, 1);
            var hint = new TextBlock { Text = "Отметка низа отсчитывается от уровня отверстия. При округлении низа высота увеличивается, чтобы сохранить проём. 0 отключает округление для этого поля.", Foreground = Brushes.LightGray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            Add(grid, hint, 2, 0);
            Grid.SetColumnSpan(hint, 2);
            foreach (var box in new[] { _size, _bottom })
            {
                box.Background = new SolidColorBrush(Color.FromRgb(36, 36, 43));
                box.Foreground = Brushes.White;
                box.CaretBrush = Brushes.White;
                box.Padding = new Thickness(5, 3, 5, 3);
            }
            Content = grid;
        }

        private static void Add(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow(element, row); Grid.SetColumn(element, column); grid.Children.Add(element);
        }

        public bool TryGetOptions(out OpeningRoundingOptions options)
        {
            options = null;
            if (!OpeningRoundingOptions.TryParseStep(_size.Text, out double size) ||
                !OpeningRoundingOptions.TryParseStep(_bottom.Text, out double bottom))
            {
                MessageBox.Show(Window.GetWindow(this), "Введите шаги округления в миллиметрах: 0 или положительное число.", "Округление", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            options = new OpeningRoundingOptions { SizeStepMm = size, BottomStepMm = bottom };
            return true;
        }
    }
}
