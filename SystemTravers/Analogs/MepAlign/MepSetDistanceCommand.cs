using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using WpfGrid = System.Windows.Controls.Grid;

namespace BimboClub.Analogs.MepAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MepSetDistanceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Нет активного документа Revit.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;

            MepSetDistanceWindow window = new MepSetDistanceWindow();
            new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

            if (window.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            double distMm = window.DistanceMm;
            bool betweenAxes = window.BetweenAxes;
            bool includeInsulation = window.IncludeInsulation;
            bool isVertical = window.IsVertical;
            bool isLoop = window.IsLoop;

            var filter = new MepAlignUtils.MepSelectionFilter(onlyCurves: true);

            do
            {
                List<MEPCurve> targetCurves = new List<MEPCurve>();
                MEPCurve baseCurve = null;

                // Проверяем предварительный выбор
                var preselected = uiDoc.Selection.GetElementIds();
                if (preselected != null && preselected.Count > 1)
                {
                    foreach (ElementId id in preselected)
                    {
                        if (doc.GetElement(id) is MEPCurve c) targetCurves.Add(c);
                    }
                }

                if (targetCurves.Count < 2)
                {
                    targetCurves.Clear();
                    try
                    {
                        IList<Reference> refs = uiDoc.Selection.PickObjects(
                            ObjectType.Element,
                            filter,
                            "Выберите MEP-кривые, для которых нужно задать расстояние (затем нажмите 'Готово')");
                        if (refs == null || refs.Count < 2) break;

                        foreach (var r in refs)
                        {
                            if (doc.GetElement(r) is MEPCurve c) targetCurves.Add(c);
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                }

                if (targetCurves.Count < 2) break;

                // Выбор базовой MEP кривой
                try
                {
                    Reference baseRef = uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        filter,
                        "Выберите базовую MEP-кривую (от которой отсчитывать расстояние)");
                    if (baseRef == null) break;
                    baseCurve = doc.GetElement(baseRef) as MEPCurve;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                if (baseCurve == null) break;

                // Исключаем базовую кривую из перемещаемых
                targetCurves = targetCurves.Where(c => c.Id != baseCurve.Id).ToList();
                if (targetCurves.Count == 0) continue;

                var baseAxis = MepAlignUtils.GetAxisInfo(baseCurve, null);
                if (baseAxis == null) continue;

                double reqDistFeet = distMm * MepAlignUtils.MmToFeet;
                double baseHalfDim = MepAlignUtils.GetHalfDimension(baseCurve, isVertical);
                double baseInsul = includeInsulation ? MepAlignUtils.GetInsulationThickness(doc, baseCurve) : 0;

                using (Transaction tr = new Transaction(doc, "Задать расстояние между MEP"))
                {
                    tr.Start();
                    int moved = 0;

                    foreach (MEPCurve target in targetCurves)
                    {
                        if (target.Pinned) continue;

                        var targetAxis = MepAlignUtils.GetAxisInfo(target, null);
                        if (targetAxis == null) continue;

                        double targetHalfDim = MepAlignUtils.GetHalfDimension(target, isVertical);
                        double targetInsul = includeInsulation ? MepAlignUtils.GetInsulationThickness(doc, target) : 0;

                        double totalTargetSpacing = betweenAxes
                            ? reqDistFeet
                            : (reqDistFeet + baseHalfDim + targetHalfDim + baseInsul + targetInsul);

                        XYZ moveVector = XYZ.Zero;

                        if (isVertical)
                        {
                            // Смещение по вертикали (Z)
                            double currentDiffZ = targetAxis.Origin.Z - baseAxis.Origin.Z;
                            double sign = currentDiffZ < -1e-4 ? -1.0 : 1.0;
                            double desiredZ = baseAxis.Origin.Z + sign * totalTargetSpacing;
                            double deltaZ = desiredZ - targetAxis.Origin.Z;

                            if (Math.Abs(deltaZ) > 1e-5)
                            {
                                moveVector = new XYZ(0, 0, deltaZ);
                            }
                        }
                        else
                        {
                            // Смещение по горизонтали (в плоскости XY перпендикулярно базовой кривой)
                            XYZ dir2D = new XYZ(baseAxis.Direction.X, baseAxis.Direction.Y, 0);
                            if (dir2D.GetLength() < 1e-4) continue;
                            dir2D = dir2D.Normalize();

                            XYZ normal2D = new XYZ(-dir2D.Y, dir2D.X, 0).Normalize();

                            // Текущее смещение центра целевого элемента относительно базового вдоль нормали
                            XYZ diffPt = targetAxis.Origin - baseAxis.Origin;
                            double currentOffset = diffPt.DotProduct(normal2D);
                            double sign = currentOffset < -1e-4 ? -1.0 : 1.0;

                            double desiredOffset = sign * totalTargetSpacing;
                            double deltaOffset = desiredOffset - currentOffset;

                            if (Math.Abs(deltaOffset) > 1e-5)
                            {
                                moveVector = normal2D.Multiply(deltaOffset);
                            }
                        }

                        if (moveVector.GetLength() > 1e-5)
                        {
                            ElementTransformUtils.MoveElement(doc, target.Id, moveVector);
                            moved++;
                        }
                    }

                    tr.Commit();
                }

                // Сбрасываем выбор элементов для следующей итерации цикла
                uiDoc.Selection.SetElementIds(new List<ElementId>());

            } while (isLoop);

            return Result.Succeeded;
        }
    }

    public class MepSetDistanceWindow : Window
    {
        private System.Windows.Controls.TextBox _txtDistance;
        private RadioButton _rbAxes;
        private RadioButton _rbFaces;
        private CheckBox _chkInsulation;
        private RadioButton _rbHorizontal;
        private RadioButton _rbVertical;
        private CheckBox _chkLoop;

        public double DistanceMm
        {
            get
            {
                if (double.TryParse(_txtDistance.Text.Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return Math.Max(0, val);
                }
                return 100.0;
            }
        }

        public bool BetweenAxes => _rbAxes.IsChecked == true;
        public bool IncludeInsulation => _chkInsulation.IsEnabled && _chkInsulation.IsChecked == true;
        public bool IsVertical => _rbVertical.IsChecked == true;
        public bool IsLoop => _chkLoop.IsChecked == true;

        public MepSetDistanceWindow()
        {
            InitializeUi();
        }

        private void InitializeUi()
        {
            Title = "Задать расстояние MEP — BimboClub";
            Width = 430;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            var bgCard = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38));
            var borderCard = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 56));
            var accentRed = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));
            var textMuted = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 200));

            StackPanel mainStack = new StackPanel { Orientation = Orientation.Vertical };
            Content = mainStack;

            // Заголовок
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(18, 18, 20),
                    System.Windows.Media.Color.FromRgb(179, 14, 45), 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            StackPanel headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "ЗАДАТЬ РАССТОЯНИЕ MEP",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Точная установка шага между осями или гранями коммуникаций",
                FontSize = 11,
                Foreground = textMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBorder.Child = headerStack;
            mainStack.Children.Add(headerBorder);

            // Карточка настроек
            Border card = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(14, 12, 14, 8),
                Padding = new Thickness(14)
            };
            StackPanel cardStack = new StackPanel();
            card.Child = cardStack;
            mainStack.Children.Add(card);

            // 1. Поле ввода расстояния + быстрые кнопки
            cardStack.Children.Add(new TextBlock
            {
                Text = "Расстояние (мм):",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            WpfGrid distGrid = new WpfGrid();
            distGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            distGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtDistance = new System.Windows.Controls.TextBox
            {
                Text = "100",
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            WpfGrid.SetColumn(_txtDistance, 0);
            distGrid.Children.Add(_txtDistance);
            cardStack.Children.Add(distGrid);

            // Быстрые пресеты
            WrapPanel presetPanel = new WrapPanel { Margin = new Thickness(0, 6, 0, 12) };
            int[] presets = new int[] { 50, 100, 150, 200, 250, 300 };
            foreach (int p in presets)
            {
                Button btnPreset = new Button
                {
                    Content = p + " мм",
                    Margin = new Thickness(0, 0, 6, 4),
                    Padding = new Thickness(8, 2, 8, 2),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 60)),
                    Foreground = Brushes.LightGray,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                int val = p;
                btnPreset.Click += (s, e) => _txtDistance.Text = val.ToString();
                presetPanel.Children.Add(btnPreset);
            }
            cardStack.Children.Add(presetPanel);

            // 2. База отсчета: Между осями / Между гранями
            cardStack.Children.Add(new TextBlock
            {
                Text = "Способ отсчета расстояния:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 6)
            });

            _rbAxes = new RadioButton
            {
                Content = "Между осями элементов",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };
            _rbFaces = new RadioButton
            {
                Content = "Между внешними гранями элементов",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };
            _chkInsulation = new CheckBox
            {
                Content = "С учетом толщины изоляции",
                IsChecked = true,
                IsEnabled = false,
                Foreground = textMuted,
                Margin = new Thickness(20, 0, 0, 10),
                Cursor = Cursors.Hand
            };

            _rbAxes.Checked += (s, e) => { _chkInsulation.IsEnabled = false; };
            _rbFaces.Checked += (s, e) => { _chkInsulation.IsEnabled = true; };

            cardStack.Children.Add(_rbAxes);
            cardStack.Children.Add(_rbFaces);
            cardStack.Children.Add(_chkInsulation);

            // 3. Направление смещения: По горизонтали / По вертикали
            cardStack.Children.Add(new TextBlock
            {
                Text = "Направление смещения:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 6)
            });

            StackPanel dirStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _rbHorizontal = new RadioButton
            {
                Content = "По горизонтали",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 18, 0),
                Cursor = Cursors.Hand
            };
            _rbVertical = new RadioButton
            {
                Content = "По вертикали",
                Foreground = Brushes.White,
                Cursor = Cursors.Hand
            };
            dirStack.Children.Add(_rbHorizontal);
            dirStack.Children.Add(_rbVertical);
            cardStack.Children.Add(dirStack);

            // 4. Опция "Выполнять циклично"
            _chkLoop = new CheckBox
            {
                Content = "Выполнять циклично (до нажатия ESC)",
                IsChecked = false,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 0),
                Cursor = Cursors.Hand
            };
            cardStack.Children.Add(_chkLoop);

            // Нижняя панель кнопок
            Border footerBorder = new Border
            {
                Padding = new Thickness(14, 6, 14, 14)
            };
            WpfGrid btnGrid = new WpfGrid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Height = 32,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 66)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => DialogResult = false;
            WpfGrid.SetColumn(btnCancel, 0);

            Button btnOk = new Button
            {
                Content = "Продолжить",
                Height = 32,
                Background = accentRed,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) => DialogResult = true;
            WpfGrid.SetColumn(btnOk, 2);

            btnGrid.Children.Add(btnCancel);
            btnGrid.Children.Add(btnOk);
            footerBorder.Child = btnGrid;
            mainStack.Children.Add(footerBorder);
        }
    }
}
