using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BimboClub
{
    public static class UiThemeHelper
    {
        public static void StyleComboBoxDark(ComboBox cb, SolidColorBrush bg, SolidColorBrush borderBrush)
        {
            cb.Background = bg;
            cb.Foreground = Brushes.White;
            cb.BorderBrush = borderBrush;

            try
            {
                Style itemStyle = new Style(typeof(ComboBoxItem));
                itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, bg));
                itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
                itemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(6, 4, 6, 4)));

                ControlTemplate template = new ControlTemplate(typeof(ComboBoxItem));
                FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
                border.Name = "Bd";
                border.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
                border.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
                border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
                border.SetValue(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

                FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(contentPresenter);
                template.VisualTree = border;

                Trigger isSelectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
                isSelectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(179, 14, 45)), "Bd"));
                isSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
                template.Triggers.Add(isSelectedTrigger);

                Trigger isHighlightedTrigger = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
                isHighlightedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(199, 16, 50)), "Bd"));
                isHighlightedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
                template.Triggers.Add(isHighlightedTrigger);

                itemStyle.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, template));
                cb.Resources[typeof(ComboBoxItem)] = itemStyle;

                cb.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Color.FromRgb(179, 14, 45));
                cb.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
                cb.Resources[SystemColors.ControlBrushKey] = bg;
            }
            catch { }

            Style tbStyle = new Style(typeof(TextBox));
            try
            {
                tbStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, bg));
                tbStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
                tbStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, Brushes.White));
                tbStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
                tbStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(2)));

                // Trigger for IsFocused
                Trigger focusTrigger = new Trigger { Property = TextBox.IsFocusedProperty, Value = true };
                focusTrigger.Setters.Add(new Setter(TextBox.BackgroundProperty, bg));
                focusTrigger.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
                tbStyle.Triggers.Add(focusTrigger);

                // Trigger for IsMouseOver
                Trigger hoverTrigger = new Trigger { Property = TextBox.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(TextBox.BackgroundProperty, bg));
                hoverTrigger.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
                tbStyle.Triggers.Add(hoverTrigger);

                cb.Resources[typeof(TextBox)] = tbStyle;
                cb.Resources[new System.Windows.ComponentResourceKey(typeof(System.Windows.Controls.ComboBox), "EditableTextBoxStyleKey")] = tbStyle;
            }
            catch { }

            cb.Loaded += (s, e) =>
            {
                try
                {
                    // Find the main root Border of the ComboBox and style it locally
                    var rootBorder = FindVisualChild<Border>(cb);
                    if (rootBorder != null)
                    {
                        rootBorder.Background = bg;
                        rootBorder.BorderBrush = borderBrush;
                    }
                }
                catch { }

                try
                {
                    var toggleButton = FindVisualChild<System.Windows.Controls.Primitives.ToggleButton>(cb);
                    if (toggleButton != null)
                    {
                        toggleButton.Background = bg;
                        toggleButton.BorderBrush = borderBrush;
                        
                        var border = FindVisualChild<Border>(toggleButton);
                        if (border != null)
                        {
                            border.Background = bg;
                            border.BorderBrush = borderBrush;
                        }

                        var path = FindVisualChild<System.Windows.Shapes.Path>(toggleButton);
                        if (path != null)
                        {
                            path.Fill = Brushes.White;
                        }
                    }
                }
                catch { }

                try
                {
                    var textBox = FindVisualChild<TextBox>(cb);
                    if (textBox != null)
                    {
                        textBox.Style = tbStyle;
                    }
                }
                catch { }
            };
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T tChild)
                {
                    return tChild;
                }
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
