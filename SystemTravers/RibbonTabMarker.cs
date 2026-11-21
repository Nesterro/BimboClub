using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BimboClub
{
    internal static class RibbonTabMarker
    {
        private const string MarkerName = "BimboClubTabDot";

        internal static void Schedule(string tabName)
        {
            int attempts = 0;
            var timer = new DispatcherTimer(DispatcherPriority.ContextIdle)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += (sender, args) =>
            {
                try
                {
                    if (TryApply(tabName) || ++attempts == 10) timer.Stop();
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    Logger.LogError("Оформление вкладки BimboClub", ex);
                }
            };
            timer.Start();
        }

        private static bool TryApply(string tabName)
        {
            var windows = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "AdWindows");
            var componentManager = windows?.GetType("Autodesk.Windows.ComponentManager");
            var ribbon = componentManager?.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Visual;
            if (ribbon == null) return false;

            var visualTree = new Stack<DependencyObject>();
            visualTree.Push(ribbon);
            while (visualTree.Count > 0)
            {
                var visual = visualTree.Pop();
                if (visual is ContentControl button && button.GetType().Name == "RibbonTabButton")
                {
                    var tab = button.DataContext;
                    var title = tab?.GetType().GetProperty("Title")?.GetValue(tab) as string;
                    if (title == tabName && button.Content is StackPanel header)
                    {
                        if (!header.Children.OfType<Ellipse>().Any(dot => dot.Name == MarkerName))
                        {
                            header.Children.Insert(0, new Ellipse
                            {
                                Name = MarkerName,
                                Width = 8,
                                Height = 8,
                                Fill = UiThemeHelper.BrandPink,
                                Margin = new Thickness(0, 0, 6, 0),
                                VerticalAlignment = VerticalAlignment.Center,
                                IsHitTestVisible = false
                            });
                        }
                        return true;
                    }
                }

                for (int index = 0; index < VisualTreeHelper.GetChildrenCount(visual); index++)
                {
                    visualTree.Push(VisualTreeHelper.GetChild(visual, index));
                }
            }

            return false;
        }
    }
}
