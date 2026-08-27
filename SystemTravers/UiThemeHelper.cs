using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace BimboClub
{
    public static class UiThemeHelper
    {
        public static void ApplyDarkTheme(Window window)
        {
            if (window == null) return;

            SolidColorBrush darkBg = new SolidColorBrush(Color.FromRgb(38, 38, 44));
            SolidColorBrush popupBg = new SolidColorBrush(Color.FromRgb(30, 30, 36));
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 70));
            SolidColorBrush accentRed = new SolidColorBrush(Color.FromRgb(179, 14, 45));
            SolidColorBrush accentRedHover = new SolidColorBrush(Color.FromRgb(199, 16, 50));

            // Overriding SystemColors resources so built-in controls use dark background and white text
            window.Resources[SystemColors.WindowBrushKey] = popupBg;
            window.Resources[SystemColors.WindowTextBrushKey] = Brushes.White;
            window.Resources[SystemColors.ControlBrushKey] = darkBg;
            window.Resources[SystemColors.ControlTextBrushKey] = Brushes.White;
            window.Resources[SystemColors.HighlightBrushKey] = accentRed;
            window.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;

            // Global ComboBoxItem Style
            Style itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, popupBg));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(8, 6, 8, 6)));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            ControlTemplate itemTemplate = new ControlTemplate(typeof(ComboBoxItem));
            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "Bd";
            itemBorder.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            itemBorder.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            itemBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            itemBorder.SetValue(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            itemContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            itemContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            itemBorder.AppendChild(itemContent);
            itemTemplate.VisualTree = itemBorder;

            Trigger isSelectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            isSelectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, accentRed, "Bd"));
            isSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            itemTemplate.Triggers.Add(isSelectedTrigger);

            Trigger isHighlightedTrigger = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            isHighlightedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, accentRedHover, "Bd"));
            isHighlightedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            itemTemplate.Triggers.Add(isHighlightedTrigger);

            itemStyle.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, itemTemplate));
            window.Resources[typeof(ComboBoxItem)] = itemStyle;

            // Global ComboBox Style
            window.Resources[typeof(ComboBox)] = CreateDarkComboBoxStyle(darkBg, popupBg, borderBrush, accentRed);

            // Global TabItem Style (Scarlet Red contrast theme)
            window.Resources[typeof(TabItem)] = CreateScarletTabItemStyle();
        }

        public static Style CreateScarletTabItemStyle()
        {
            Style style = new Style(typeof(TabItem));
            SolidColorBrush unselectedBg = new SolidColorBrush(Color.FromRgb(40, 40, 48));
            SolidColorBrush selectedBg = new SolidColorBrush(Color.FromRgb(179, 14, 45));   // Scarlet Red #B30E2D
            SolidColorBrush hoverBg = new SolidColorBrush(Color.FromRgb(199, 16, 50));      // Bright Scarlet Red #C71032

            style.Setters.Add(new Setter(TabItem.BackgroundProperty, unselectedBg));
            style.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(16, 8, 16, 8)));
            style.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(0, 0, 4, 0)));
            style.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.Medium));

            ControlTemplate template = new ControlTemplate(typeof(TabItem));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bd";
            border.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4, 4, 0, 0));
            border.SetValue(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            // Trigger for IsSelected = true
            Trigger selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, selectedBg, "Bd"));
            selectedTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            selectedTrigger.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.Bold));
            template.Triggers.Add(selectedTrigger);

            // Trigger for IsMouseOver = true (when not selected)
            MultiTrigger hoverTrigger = new MultiTrigger();
            hoverTrigger.Conditions.Add(new Condition(TabItem.IsMouseOverProperty, true));
            hoverTrigger.Conditions.Add(new Condition(TabItem.IsSelectedProperty, false));
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
            hoverTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(TabItem.TemplateProperty, template));
            return style;
        }

        public static Style CreateDarkComboBoxItemStyle(SolidColorBrush popupBg, SolidColorBrush accentColor)
        {
            Style itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, popupBg));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(8, 6, 8, 6)));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.CursorProperty, Cursors.Hand));

            ControlTemplate itemTemplate = new ControlTemplate(typeof(ComboBoxItem));
            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "Bd";
            itemBorder.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            itemBorder.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            itemBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            itemBorder.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));
            itemBorder.SetValue(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            itemContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            itemContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            
            itemBorder.AppendChild(itemContent);
            itemTemplate.VisualTree = itemBorder;

            Trigger isSelectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            isSelectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, accentColor ?? new SolidColorBrush(Color.FromRgb(79, 70, 229)), "Bd"));
            isSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            itemTemplate.Triggers.Add(isSelectedTrigger);

            Trigger isHighlightedTrigger = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            isHighlightedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(56, 56, 75)), "Bd"));
            isHighlightedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(255, 215, 0))));
            itemTemplate.Triggers.Add(isHighlightedTrigger);

            itemStyle.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, itemTemplate));
            return itemStyle;
        }

        public static Style CreateDarkComboBoxStyle(SolidColorBrush bg, SolidColorBrush popupBg, SolidColorBrush borderBrush, SolidColorBrush accentRed)
        {
            Style cbStyle = new Style(typeof(ComboBox));
            cbStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, bg));
            cbStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            cbStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, borderBrush));
            cbStyle.Setters.Add(new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)));
            cbStyle.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(8, 4, 8, 4)));
            cbStyle.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, CreateDarkComboBoxItemStyle(popupBg, accentRed)));

            // ToggleButton Template
            ControlTemplate toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            FrameworkElementFactory toggleGrid = new FrameworkElementFactory(typeof(Grid));
            
            FrameworkElementFactory toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.Name = "Border";
            toggleBorder.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            toggleBorder.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            toggleBorder.SetValue(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            toggleGrid.AppendChild(toggleBorder);

            FrameworkElementFactory arrowPath = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            arrowPath.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 0 L 4 4 L 8 0 Z"));
            arrowPath.SetValue(System.Windows.Shapes.Path.FillProperty, new SolidColorBrush(Color.FromRgb(200, 200, 220)));
            arrowPath.SetValue(System.Windows.Shapes.Path.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrowPath.SetValue(System.Windows.Shapes.Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrowPath.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 0, 10, 0));
            toggleGrid.AppendChild(arrowPath);

            toggleTemplate.VisualTree = toggleGrid;

            // Main ComboBox ControlTemplate
            ControlTemplate cbTemplate = new ControlTemplate(typeof(ComboBox));
            FrameworkElementFactory mainGrid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory toggleBtn = new FrameworkElementFactory(typeof(ToggleButton));
            toggleBtn.Name = "ToggleButton";
            toggleBtn.SetValue(ToggleButton.TemplateProperty, toggleTemplate);
            toggleBtn.SetValue(ToggleButton.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            toggleBtn.SetValue(ToggleButton.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            toggleBtn.SetValue(ToggleButton.FocusableProperty, false);
            toggleBtn.SetValue(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { Mode = BindingMode.TwoWay, RelativeSource = RelativeSource.TemplatedParent });
            mainGrid.AppendChild(toggleBtn);

            FrameworkElementFactory contentPres = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPres.Name = "ContentSite";
            contentPres.SetValue(ContentPresenter.IsHitTestVisibleProperty, false);
            contentPres.SetValue(ContentPresenter.ContentProperty, new Binding("SelectionBoxItem") { RelativeSource = RelativeSource.TemplatedParent });
            contentPres.SetValue(ContentPresenter.ContentTemplateProperty, new Binding("SelectionBoxItemTemplate") { RelativeSource = RelativeSource.TemplatedParent });
            contentPres.SetValue(ContentPresenter.ContentTemplateSelectorProperty, new Binding("ItemTemplateSelector") { RelativeSource = RelativeSource.TemplatedParent });
            contentPres.SetValue(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
            contentPres.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPres.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            mainGrid.AppendChild(contentPres);

            // Popup
            FrameworkElementFactory popup = new FrameworkElementFactory(typeof(Popup));
            popup.Name = "PART_Popup";
            popup.SetValue(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent });
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.FocusableProperty, false);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Slide);

            FrameworkElementFactory popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.Name = "DropDownBorder";
            popupBorder.SetValue(Border.BackgroundProperty, popupBg);
            popupBorder.SetValue(Border.BorderBrushProperty, borderBrush);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 2, 0, 0));

            FrameworkElementFactory scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewer.SetValue(ScrollViewer.MarginProperty, new Thickness(1));
            scrollViewer.SetValue(ScrollViewer.SnapsToDevicePixelsProperty, true);
            scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);

            FrameworkElementFactory itemsHost = new FrameworkElementFactory(typeof(StackPanel));
            itemsHost.SetValue(StackPanel.IsItemsHostProperty, true);
            itemsHost.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Contained);

            scrollViewer.AppendChild(itemsHost);
            popupBorder.AppendChild(scrollViewer);
            popup.AppendChild(popupBorder);
            mainGrid.AppendChild(popup);

            cbTemplate.VisualTree = mainGrid;
            cbStyle.Setters.Add(new Setter(ComboBox.TemplateProperty, cbTemplate));

            return cbStyle;
        }

        public static void StyleComboBoxDark(ComboBox cb, SolidColorBrush bg, SolidColorBrush borderBrush)
        {
            if (cb == null) return;

            SolidColorBrush popupBg = new SolidColorBrush(Color.FromRgb(30, 30, 40));
            SolidColorBrush accentRed = new SolidColorBrush(Color.FromRgb(179, 14, 45));
            SolidColorBrush border = borderBrush ?? new SolidColorBrush(Color.FromRgb(60, 60, 75));
            SolidColorBrush inputBg = bg ?? new SolidColorBrush(Color.FromRgb(38, 38, 48));

            cb.Style = CreateDarkComboBoxStyle(inputBg, popupBg, border, accentRed);
        }
    }
}
