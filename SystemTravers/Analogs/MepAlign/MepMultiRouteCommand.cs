using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using WpfGrid = System.Windows.Controls.Grid;

namespace BimboClub.Analogs.MepAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MepMultiRouteCommand : IExternalCommand
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
            var filter = new MepAlignUtils.MepSelectionFilter(onlyCurves: true);

            // 1. Выбор параллельных MEP кривых
            List<MEPCurve> curves = new List<MEPCurve>();
            var preselected = uiDoc.Selection.GetElementIds();
            if (preselected != null && preselected.Count > 1)
            {
                foreach (ElementId id in preselected)
                {
                    if (doc.GetElement(id) is MEPCurve c) curves.Add(c);
                }
            }

            if (curves.Count < 2)
            {
                curves.Clear();
                try
                {
                    IList<Reference> refs = uiDoc.Selection.PickObjects(
                        ObjectType.Element,
                        filter,
                        "Выберите параллельные MEP-кривые для мультипостроения (затем нажмите 'Готово')");
                    if (refs == null || refs.Count < 2) return Result.Cancelled;

                    foreach (var r in refs)
                    {
                        if (doc.GetElement(r) is MEPCurve c) curves.Add(c);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            if (curves.Count < 2) return Result.Cancelled;

            // 2. Выбор базовой MEP кривой
            MEPCurve baseCurve = null;
            try
            {
                Reference baseRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    "Выберите базовую MEP-кривую (для указания направления построения)");
                if (baseRef == null) return Result.Cancelled;
                baseCurve = doc.GetElement(baseRef) as MEPCurve;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (baseCurve == null) return Result.Cancelled;

            // 3. Окно настроек мультипостроения
            MepMultiRouteWindow window = new MepMultiRouteWindow();
            new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
            if (window.ShowDialog() != true) return Result.Cancelled;

            double? fixedAngle = window.SelectedAngle; // null - свободный угол
            bool groupTransactions = window.GroupTransactions;

            TransactionGroup tg = null;
            if (groupTransactions)
            {
                tg = new TransactionGroup(doc, "Мультипостроение MEP");
                tg.Start();
            }

            try
            {
                List<MEPCurve> currentCurves = new List<MEPCurve>(curves);
                MEPCurve currentBase = baseCurve;

                while (true)
                {
                    XYZ pickPoint;
                    try
                    {
                        pickPoint = uiDoc.Selection.PickPoint(
                            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                            "Укажите точку направления продолжения трассы [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (pickPoint == null) break;

                    // Находим открытые концы текущих кривых, направленные в сторону указанной точки
                    LocationCurve baseLoc = currentBase.Location as LocationCurve;
                    if (baseLoc == null) break;

                    XYZ baseP0 = baseLoc.Curve.GetEndPoint(0);
                    XYZ baseP1 = baseLoc.Curve.GetEndPoint(1);

                    // Выбираем конец базовой кривой, ближайший к точке клика
                    bool useP1 = baseP1.DistanceTo(pickPoint) < baseP0.DistanceTo(pickPoint);
                    XYZ baseEndPt = useP1 ? baseP1 : baseP0;
                    XYZ baseDir = useP1 ? (baseP1 - baseP0).Normalize() : (baseP0 - baseP1).Normalize();

                    // Вектор к точке клика
                    XYZ newDir = (pickPoint - baseEndPt);
                    if (newDir.GetLength() < 0.05) continue; // Слишком близко

                    // Учитываем фиксацию углов (если задано)
                    if (fixedAngle.HasValue)
                    {
                        double targetRad = fixedAngle.Value * Math.PI / 180.0;
                        XYZ baseDir2D = new XYZ(baseDir.X, baseDir.Y, 0).Normalize();
                        XYZ newDir2D = new XYZ(newDir.X, newDir.Y, 0).Normalize();

                        double angle = baseDir2D.AngleTo(newDir2D);
                        // Округляем до ближайшего кратного угла
                        double snappedAngle = Math.Round(angle / targetRad) * targetRad;
                        if (Math.Abs(snappedAngle) < 1e-4) snappedAngle = targetRad;

                        // Поворачиваем baseDir2D на snappedAngle с учетом знака векторного произведения
                        double crossZ = baseDir2D.X * newDir2D.Y - baseDir2D.Y * newDir2D.X;
                        double sign = crossZ < 0 ? -1.0 : 1.0;
                        double finalAngle = sign * snappedAngle;

                        double cos = Math.Cos(finalAngle);
                        double sin = Math.Sin(finalAngle);
                        XYZ rotated2D = new XYZ(baseDir2D.X * cos - baseDir2D.Y * sin, baseDir2D.X * sin + baseDir2D.Y * cos, 0).Normalize();

                        // Проекция длины newDir на повернутое направление
                        double len = newDir.GetLength();
                        newDir = rotated2D.Multiply(len);
                    }

                    // Транзакция для шага построения
                    using (Transaction tr = new Transaction(doc, "Шаг мультипостроения MEP"))
                    {
                        tr.Start();

                        List<MEPCurve> nextGeneration = new List<MEPCurve>();
                        MEPCurve nextBase = null;

                        foreach (MEPCurve cur in currentCurves)
                        {
                            LocationCurve lc = cur.Location as LocationCurve;
                            if (lc == null) continue;

                            XYZ p0 = lc.Curve.GetEndPoint(0);
                            XYZ p1 = lc.Curve.GetEndPoint(1);

                            // Конец в сторону построения
                            XYZ startPt = p1.DistanceTo(pickPoint) < p0.DistanceTo(pickPoint) ? p1 : p0;
                            XYZ endPt = startPt + newDir;

                            Connector openConn = GetOpenConnectorNear(cur, startPt);

                            MEPCurve newSeg = CreateContinuationSegment(doc, cur, startPt, endPt);
                            if (newSeg != null)
                            {
                                nextGeneration.Add(newSeg);
                                if (cur.Id == currentBase.Id) nextBase = newSeg;

                                // Соединяем отводом
                                if (openConn != null)
                                {
                                    Connector newConn = GetOpenConnectorNear(newSeg, startPt);
                                    if (newConn != null)
                                    {
                                        try
                                        {
                                            doc.Create.NewElbowFitting(openConn, newConn);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }

                        tr.Commit();

                        if (nextGeneration.Count > 0)
                        {
                            currentCurves = nextGeneration;
                            if (nextBase != null) currentBase = nextBase;
                        }
                    }
                }

                if (tg != null) tg.Assimilate();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                if (tg != null) tg.RollBack();
                Logger.LogError("Ошибка в команде Мультипостроение", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static Connector GetOpenConnectorNear(Element elem, XYZ pt)
        {
            ConnectorSet connectors = null;
            if (elem is MEPCurve mc) connectors = mc.ConnectorManager?.Connectors;
            else if (elem is FamilyInstance fi && fi.MEPModel != null) connectors = fi.MEPModel.ConnectorManager?.Connectors;

            if (connectors == null) return null;

            Connector closest = null;
            double minDist = double.MaxValue;

            foreach (Connector c in connectors)
            {
                if (c == null || c.ConnectorType == ConnectorType.Logical || c.IsConnected) continue;
                double d = c.Origin.DistanceTo(pt);
                if (d < minDist)
                {
                    minDist = d;
                    closest = c;
                }
            }

            return closest;
        }

        private static MEPCurve CreateContinuationSegment(Document doc, MEPCurve original, XYZ start, XYZ end)
        {
            if (start.DistanceTo(end) < 0.01) return null;

            ElementId typeId = original.GetTypeId();
            ElementId levelId = original.ReferenceLevel != null ? original.ReferenceLevel.Id : ElementId.InvalidElementId;
            if (levelId == ElementId.InvalidElementId)
            {
                Parameter pLevel = original.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                if (pLevel != null && pLevel.HasValue) levelId = pLevel.AsElementId();
            }

            try
            {
                if (original is Pipe origPipe)
                {
                    ElementId sysTypeId = origPipe.MEPSystem != null ? origPipe.MEPSystem.GetTypeId() : ElementId.InvalidElementId;
                    if (sysTypeId == ElementId.InvalidElementId)
                    {
                        Parameter pSys = origPipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
                        if (pSys != null && pSys.HasValue) sysTypeId = pSys.AsElementId();
                    }

                    Pipe newPipe = Pipe.Create(doc, sysTypeId, typeId, levelId, start, end);
                    if (newPipe != null)
                    {
                        Parameter dParam = newPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                        if (dParam != null && !dParam.IsReadOnly)
                        {
                            dParam.Set(origPipe.Diameter);
                        }
                        return newPipe;
                    }
                }
                else if (original is Duct origDuct)
                {
                    ElementId sysTypeId = origDuct.MEPSystem != null ? origDuct.MEPSystem.GetTypeId() : ElementId.InvalidElementId;
                    if (sysTypeId == ElementId.InvalidElementId)
                    {
                        Parameter pSys = origDuct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                        if (pSys != null && pSys.HasValue) sysTypeId = pSys.AsElementId();
                    }

                    Duct newDuct = Duct.Create(doc, sysTypeId, typeId, levelId, start, end);
                    if (newDuct != null)
                    {
                        Parameter wOrig = origDuct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                        Parameter hOrig = origDuct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                        Parameter dOrig = origDuct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

                        if (wOrig != null && hOrig != null && wOrig.HasValue && hOrig.HasValue)
                        {
                            newDuct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(wOrig.AsDouble());
                            newDuct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(hOrig.AsDouble());
                        }
                        else if (dOrig != null && dOrig.HasValue)
                        {
                            newDuct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.Set(dOrig.AsDouble());
                        }
                        return newDuct;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка создания продолжения сегмента", ex);
            }

            return null;
        }
    }

    public class MepMultiRouteWindow : Window
    {
        private RadioButton _rbFree;
        private RadioButton _rb90;
        private RadioButton _rb45;
        private RadioButton _rb30;
        private CheckBox _chkGroupTransactions;

        public double? SelectedAngle
        {
            get
            {
                if (_rb90.IsChecked == true) return 90.0;
                if (_rb45.IsChecked == true) return 45.0;
                if (_rb30.IsChecked == true) return 30.0;
                return null; // Свободный угол
            }
        }

        public bool GroupTransactions => _chkGroupTransactions.IsChecked == true;

        public MepMultiRouteWindow()
        {
            InitializeUi();
        }

        private void InitializeUi()
        {
            Title = "Мультипостроение MEP — BimboClub";
            Width = 400;
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
                Text = "МУЛЬТИПОСТРОЕНИЕ MEP",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Параллельная прокладка нескольких трасс с авто-отводами",
                FontSize = 11,
                Foreground = textMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBorder.Child = headerStack;
            mainStack.Children.Add(headerBorder);

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

            cardStack.Children.Add(new TextBlock
            {
                Text = "Фиксация углов поворота:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _rbFree = new RadioButton { Content = "Свободный угол (по клику)", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 6) };
            _rb90 = new RadioButton { Content = "Фиксированный угол 90°", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 6) };
            _rb45 = new RadioButton { Content = "Фиксированный угол 45°", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 6) };
            _rb30 = new RadioButton { Content = "Фиксированный угол 30°", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) };

            cardStack.Children.Add(_rbFree);
            cardStack.Children.Add(_rb90);
            cardStack.Children.Add(_rb45);
            cardStack.Children.Add(_rb30);

            _chkGroupTransactions = new CheckBox
            {
                Content = "Группировать транзакции (отмена по Ctrl+Z за один раз)",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 0)
            };
            cardStack.Children.Add(_chkGroupTransactions);

            Border footerBorder = new Border { Padding = new Thickness(14, 6, 14, 14) };
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
                Cursor = System.Windows.Input.Cursors.Hand
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
                Cursor = System.Windows.Input.Cursors.Hand
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
