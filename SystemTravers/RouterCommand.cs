using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class RouterCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Запуск команды Умная трассировка (BimboClub Router).");

                // 1. Анализируем предварительный выбор (выбранные элементы)
                Element selectedMepElem = null;
                List<Element> selectedObstacles = new List<Element>();

                Selection sel = uidoc.Selection;
                List<ElementId> initialSelectedIds = sel.GetElementIds().ToList();

                List<Element> mepCandidates = new List<Element>();
                foreach (ElementId id in initialSelectedIds)
                {
                    Element el = doc.GetElement(id);
                    if (el is Duct || el is Pipe)
                    {
                        mepCandidates.Add(el);
                    }
                }

                if (mepCandidates.Count == 1)
                {
                    selectedMepElem = mepCandidates[0];
                    // Все остальные выбранные элементы являются препятствиями для обхода
                    foreach (ElementId id in initialSelectedIds)
                    {
                        if (id != selectedMepElem.Id)
                        {
                            selectedObstacles.Add(doc.GetElement(id));
                        }
                    }
                }
                else
                {
                    // Если MEP-элемент не выбран однозначно, просим пользователя его выбрать
                    try
                    {
                        Reference r = sel.PickObject(ObjectType.Element, new MepElementSelectionFilter(), "Пожалуйста, выберите воздуховод или трубу для построения обхода");
                        selectedMepElem = doc.GetElement(r);

                        // Все остальные элементы из первоначального выбора (кроме выбранного MEP) становятся препятствиями
                        foreach (ElementId id in initialSelectedIds)
                        {
                            if (id != selectedMepElem.Id)
                            {
                                selectedObstacles.Add(doc.GetElement(id));
                            }
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        Logger.Log("Выбор элемента отменен пользователем.");
                        return Result.Cancelled;
                    }
                }

                if (selectedMepElem == null)
                {
                    TaskDialog.Show("Внимание", "Не выбран корректный MEP элемент (воздуховод или труба).");
                    return Result.Cancelled;
                }

                // 2. Открываем WPF окно настроек
                RouterWindow window = new RouterWindow();
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();
                if (dialogResult != true || !window.IsExecutionRequested)
                {
                    Logger.Log("Выполнение команды отменено в интерфейсе.");
                    return Result.Cancelled;
                }

                string direction = window.SelectedDirection;
                double angleDeg = window.SelectedAngle;
                double clearanceFt = window.ClearanceMm / 304.8;
                double offsetFt = window.OffsetMm / 304.8;
                bool searchInLinks = window.SearchInLinks;
                List<BuiltInCategory> selectedCategories = window.SelectedCategories;

                // 3. Вычисляем геометрию осевой линии
                LocationCurve locCurve = selectedMepElem.Location as LocationCurve;
                if (locCurve == null || !(locCurve.Curve is Line))
                {
                    TaskDialog.Show("Ошибка", "Выбранный элемент не имеет прямой осевой линии.");
                    return Result.Failed;
                }

                Line mepLine = locCurve.Curve as Line;
                XYZ p0 = mepLine.GetEndPoint(0);
                XYZ p1 = mepLine.GetEndPoint(1);
                XYZ dir = (p1 - p0).Normalize();
                double totalLength = p0.DistanceTo(p1);

                // Собираем подключенные элементы, чтобы исключить их из коллизий
                List<ElementId> connectedIds = new List<ElementId>();
                connectedIds.Add(selectedMepElem.Id);

                ConnectorManager connManager = (selectedMepElem is Duct) 
                    ? ((Duct)selectedMepElem).ConnectorManager 
                    : ((Pipe)selectedMepElem).ConnectorManager;

                if (connManager != null)
                {
                    foreach (Connector c in connManager.Connectors)
                    {
                        if (c.IsConnected)
                        {
                            foreach (Connector refConn in c.AllRefs)
                            {
                                if (refConn.Owner != null && refConn.Owner.Id != selectedMepElem.Id)
                                {
                                    connectedIds.Add(refConn.Owner.Id);
                                }
                            }
                        }
                    }
                }

                // 4. Поиск коллизий (физических пересечений или выбранных препятствий)
                List<ObstacleInfo> obstacles = new List<ObstacleInfo>();
                if (selectedObstacles.Count > 0)
                {
                    Logger.Log($"Используются выбранные элементы в качестве препятствий: {selectedObstacles.Count} шт.");
                    foreach (Element el in selectedObstacles)
                    {
                        obstacles.Add(new ObstacleInfo(el, Transform.Identity));
                    }
                }
                else
                {
                    obstacles = FindObstacles(doc, mepLine, searchInLinks, selectedCategories, connectedIds);
                }

                List<ClashInterval> rawIntervals = GetClashIntervals(mepLine, obstacles, totalLength);
                
                // Фильтруем и зажимаем в интервал [0, L]
                List<ClashInterval> validIntervals = new List<ClashInterval>();
                foreach (var interval in rawIntervals)
                {
                    double s = Math.Max(0.0, interval.Start);
                    double e = Math.Min(totalLength, interval.End);
                    if (s < e && (e - s) > 0.001)
                    {
                        validIntervals.Add(new ClashInterval { Start = s, End = e });
                    }
                }

                if (validIntervals.Count == 0)
                {
                    TaskDialog.Show("Информация", "Коллизий с выбранными категориями препятствий не обнаружено.");
                    return Result.Cancelled;
                }

                // Сортируем интервалы по возрастанию Start
                validIntervals = validIntervals.OrderBy(i => i.Start).ToList();

                // 5. Расчет вектора смещения
                XYZ offsetDir = XYZ.BasisZ; // Вверх по умолчанию
                if (direction == "DOWN")
                {
                    offsetDir = -XYZ.BasisZ;
                }
                else if (direction == "LEFT" || direction == "RIGHT")
                {
                    XYZ leftDir = dir.CrossProduct(XYZ.BasisZ);
                    if (leftDir.IsZeroLength())
                    {
                        leftDir = XYZ.BasisX; // На случай вертикального воздуховода
                    }
                    else
                    {
                        leftDir = leftDir.Normalize();
                    }
                    offsetDir = (direction == "LEFT") ? leftDir : -leftDir;
                }

                XYZ offsetVector = offsetDir * offsetFt;

                // 6. Расчет характерных точек обхода ("утки")
                double angleRad = angleDeg * Math.PI / 180.0;
                double transitionLength = (angleDeg >= 89.9) ? 0.0 : (offsetFt / Math.Tan(angleRad));

                // Объединяем близкие интервалы
                double minGapRequired = 2 * (clearanceFt + transitionLength) + (100.0 / 304.8); // 100mm минимальный прямой участок между обходами
                List<ClashInterval> mergedIntervals = new List<ClashInterval>();
                foreach (var interval in validIntervals)
                {
                    if (mergedIntervals.Count == 0)
                    {
                        mergedIntervals.Add(interval);
                    }
                    else
                    {
                        var last = mergedIntervals[mergedIntervals.Count - 1];
                        double gap = interval.Start - last.End;
                        if (gap < minGapRequired)
                        {
                            last.End = Math.Max(last.End, interval.End);
                        }
                        else
                        {
                            mergedIntervals.Add(interval);
                        }
                    }
                }

                // Проверяем последовательность параметров на возрастание
                double currentParam = 0.0;
                bool lengthValid = true;

                for (int i = 0; i < mergedIntervals.Count; i++)
                {
                    var interval = mergedIntervals[i];
                    double paramA = interval.Start - clearanceFt - transitionLength;
                    double paramB = interval.Start - clearanceFt;
                    double paramC = interval.End + clearanceFt;
                    double paramD = interval.End + clearanceFt + transitionLength;

                    if (paramA < currentParam || paramB <= paramA || paramC <= paramB || paramD <= paramC)
                    {
                        lengthValid = false;
                        break;
                    }
                    currentParam = paramD;
                }

                if (!lengthValid || currentParam > totalLength)
                {
                    TaskDialog.Show("Внимание", "Длины выбранного элемента недостаточно для построения всех обходов с заданным углом и зазором.\n\nПожалуйста, используйте более длинный участок или увеличьте угол отвода.");
                    return Result.Cancelled;
                }

                // Строим точки
                List<XYZ> points = new List<XYZ>();
                points.Add(p0);

                for (int i = 0; i < mergedIntervals.Count; i++)
                {
                    var interval = mergedIntervals[i];
                    double paramA = interval.Start - clearanceFt - transitionLength;
                    double paramB = interval.Start - clearanceFt;
                    double paramC = interval.End + clearanceFt;
                    double paramD = interval.End + clearanceFt + transitionLength;

                    XYZ ptA = mepLine.Evaluate(paramA, false);
                    XYZ ptB = mepLine.Evaluate(paramB, false) + offsetVector;
                    XYZ ptC = mepLine.Evaluate(paramC, false) + offsetVector;
                    XYZ ptD = mepLine.Evaluate(paramD, false);

                    points.Add(ptA);
                    points.Add(ptB);
                    points.Add(ptC);
                    points.Add(ptD);
                }

                points.Add(p1);

                // 7. Сбор информации для копирования
                ElementId systemTypeId = ElementId.InvalidElementId;
                ElementId typeId = selectedMepElem.GetTypeId();
                ElementId levelId = selectedMepElem.LevelId;

                bool isDuct = selectedMepElem is Duct;
                double width = 0, height = 0, diameter = 0;

                if (isDuct)
                {
                    systemTypeId = selectedMepElem.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM).AsElementId();
                    Parameter pW = selectedMepElem.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    Parameter pH = selectedMepElem.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    Parameter pD = selectedMepElem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    
                    if (pW != null && pH != null && pW.HasValue && pH.HasValue)
                    {
                        width = pW.AsDouble();
                        height = pH.AsDouble();
                    }
                    else if (pD != null && pD.HasValue)
                    {
                        diameter = pD.AsDouble();
                    }
                }
                else
                {
                    systemTypeId = selectedMepElem.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId();
                    Parameter pD = selectedMepElem.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    if (pD != null && pD.HasValue)
                    {
                        diameter = pD.AsDouble();
                    }
                }

                // Запоминаем подключения на концах
                Connector otherConn0 = null;
                Connector otherConn1 = null;

                foreach (Connector c in connManager.Connectors)
                {
                    if (c.IsConnected)
                    {
                        double d0 = c.Origin.DistanceTo(p0);
                        double d1 = c.Origin.DistanceTo(p1);

                        Connector connected = c.AllRefs.Cast<Connector>()
                            .FirstOrDefault(r => r.ConnectorType == ConnectorType.Physical && r.Owner.Id != selectedMepElem.Id);

                        if (connected != null)
                        {
                            if (d0 < d1) otherConn0 = connected;
                            else otherConn1 = connected;
                        }
                    }
                }

                // 8. Построение элементов в транзакции
                using (Transaction tx = new Transaction(doc, "Построение обходов коллизий"))
                {
                    tx.Start();

                    // Удаляем оригинальный воздуховод/трубу
                    doc.Delete(selectedMepElem.Id);

                    // Создаем все участки
                    List<Element> createdCurves = new List<Element>();
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        XYZ startPt = points[i];
                        XYZ endPt = points[i + 1];
                        Element seg = CreateMepCurve(doc, isDuct, systemTypeId, typeId, levelId, startPt, endPt, width, height, diameter);
                        createdCurves.Add(seg);
                    }

                    // Восстанавливаем торцевые подключения исходной трубы
                    if (otherConn0 != null)
                    {
                        Connector cStart = GetConnectorNearPoint(createdCurves[0], p0);
                        if (cStart != null) cStart.ConnectTo(otherConn0);
                    }
                    if (otherConn1 != null)
                    {
                        Connector cEnd = GetConnectorNearPoint(createdCurves[createdCurves.Count - 1], p1);
                        if (cEnd != null) cEnd.ConnectTo(otherConn1);
                    }

                    // Соединяем фитингами (отводами) стыки
                    for (int i = 0; i < createdCurves.Count - 1; i++)
                    {
                        XYZ jointPt = points[i + 1];
                        Connector c1 = GetConnectorNearPoint(createdCurves[i], jointPt);
                        Connector c2 = GetConnectorNearPoint(createdCurves[i + 1], jointPt);
                        doc.Create.NewElbowFitting(c1, c2);
                    }

                    tx.Commit();
                }

                Logger.Log("Успешно построен автоматический обход коллизий.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения RouterCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private Element CreateMepCurve(Document doc, bool isDuct, ElementId systemTypeId, ElementId typeId, ElementId levelId, XYZ start, XYZ end, double w, double h, double d)
        {
            Element elem = null;
            if (isDuct)
            {
                Duct duct = Duct.Create(doc, systemTypeId, typeId, levelId, start, end);
                elem = duct;
                if (w > 0 && h > 0)
                {
                    duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(w);
                    duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(h);
                }
                else if (d > 0)
                {
                    duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM).Set(d);
                }
            }
            else
            {
                Pipe pipe = Pipe.Create(doc, systemTypeId, typeId, levelId, start, end);
                elem = pipe;
                if (d > 0)
                {
                    pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(d);
                }
            }
            return elem;
        }

        private Connector GetConnectorNearPoint(Element elem, XYZ pt)
        {
            ConnectorManager cm = null;
            if (elem is Duct) cm = ((Duct)elem).ConnectorManager;
            else if (elem is Pipe) cm = ((Pipe)elem).ConnectorManager;

            if (cm == null) return null;

            Connector nearest = null;
            double minDist = double.MaxValue;

            foreach (Connector c in cm.Connectors)
            {
                double dist = c.Origin.DistanceTo(pt);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = c;
                }
            }
            return nearest;
        }

        private List<ObstacleInfo> FindObstacles(Document doc, Line mepLine, bool includeLinks, List<BuiltInCategory> catList, List<ElementId> excludeIds)
        {
            List<ObstacleInfo> obstacles = new List<ObstacleInfo>();

            if (catList == null || catList.Count == 0) return obstacles;

            ElementMulticategoryFilter catFilter = new ElementMulticategoryFilter(catList);

            // Поиск в текущем документе с помощью BoundingBoxIntersectsFilter для быстродействия
            BoundingBoxXYZ bbox = new BoundingBoxXYZ();
            XYZ p0 = mepLine.GetEndPoint(0);
            XYZ p1 = mepLine.GetEndPoint(1);
            bbox.Min = new XYZ(Math.Min(p0.X, p1.X) - 1.0, Math.Min(p0.Y, p1.Y) - 1.0, Math.Min(p0.Z, p1.Z) - 1.0);
            bbox.Max = new XYZ(Math.Max(p0.X, p1.X) + 1.0, Math.Max(p0.Y, p1.Y) + 1.0, Math.Max(p0.Z, p1.Z) + 1.0);

            Outline outline = new Outline(bbox.Min, bbox.Max);
            BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);

            FilteredElementCollector col = new FilteredElementCollector(doc)
                .WherePasses(catFilter)
                .WherePasses(bboxFilter)
                .Excluding(excludeIds)
                .WhereElementIsNotElementType();

            foreach (Element e in col)
            {
                obstacles.Add(new ObstacleInfo(e, Transform.Identity));
            }

            // Поиск в связанных файлах
            if (includeLinks)
            {
                FilteredElementCollector linkCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance));

                foreach (RevitLinkInstance linkInst in linkCollector.Cast<RevitLinkInstance>())
                {
                    Document linkDoc = linkInst.GetLinkDocument();
                    if (linkDoc == null) continue;

                    Transform transform = linkInst.GetTotalTransform();
                    Transform invTransform = transform.Inverse;

                    // Переводим линию в систему координат связи
                    XYZ localP0 = invTransform.OfPoint(p0);
                    XYZ localP1 = invTransform.OfPoint(p1);

                    Outline localOutline = new Outline(
                        new XYZ(Math.Min(localP0.X, localP1.X) - 1.0, Math.Min(localP0.Y, localP1.Y) - 1.0, Math.Min(localP0.Z, localP1.Z) - 1.0),
                        new XYZ(Math.Max(localP0.X, localP1.X) + 1.0, Math.Max(localP0.Y, localP1.Y) + 1.0, Math.Max(localP0.Z, localP1.Z) + 1.0)
                    );
                    BoundingBoxIntersectsFilter localBboxFilter = new BoundingBoxIntersectsFilter(localOutline);

                    FilteredElementCollector linkCol = new FilteredElementCollector(linkDoc)
                        .WherePasses(catFilter)
                        .WherePasses(localBboxFilter)
                        .WhereElementIsNotElementType();

                    foreach (Element e in linkCol)
                    {
                        obstacles.Add(new ObstacleInfo(e, transform));
                    }
                }
            }

            return obstacles;
        }

        public class ClashInterval
        {
            public double Start { get; set; }
            public double End { get; set; }
        }

        private List<ClashInterval> GetClashIntervals(Line mepLine, List<ObstacleInfo> obstacles, double L)
        {
            List<ClashInterval> intervals = new List<ClashInterval>();

            foreach (ObstacleInfo obs in obstacles)
            {
                GeometryElement geom = null;
                try
                {
                    geom = obs.Element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Medium });
                }
                catch { }

                if (geom == null) continue;
                Transform trans = obs.Transform;

                List<double> obstacleParams = new List<double>();

                foreach (GeometryObject geomObj in geom)
                {
                    if (geomObj is Solid solid)
                    {
                        if (solid.Volume <= 0) continue;

                        if (!trans.IsIdentity)
                        {
                            solid = SolidUtils.CreateTransformed(solid, trans);
                        }

                        foreach (Face face in solid.Faces)
                        {
                            IntersectionResultArray results;
                            SetComparisonResult res = face.Intersect(mepLine, out results);
                            if (res == SetComparisonResult.Overlap && results != null)
                            {
                                foreach (IntersectionResult ir in results)
                                {
                                    double param = mepLine.Project(ir.XYZPoint).Parameter;
                                    obstacleParams.Add(param);
                                }
                            }
                        }
                    }
                }

                if (obstacleParams.Count > 0)
                {
                    double minP = obstacleParams.Min();
                    double maxP = obstacleParams.Max();
                    if (obstacleParams.Count == 1)
                    {
                        minP = Math.Max(0.0, minP - 0.164);
                        maxP = Math.Min(L, maxP + 0.164);
                    }
                    intervals.Add(new ClashInterval { Start = minP, End = maxP });
                }
                else
                {
                    // Fallback to bounding box projection for this obstacle
                    try
                    {
                        BoundingBoxXYZ bb = obs.Element.get_BoundingBox(null);
                        if (bb != null)
                        {
                            XYZ min = bb.Min;
                            XYZ max = bb.Max;
                            XYZ[] corners = new XYZ[]
                            {
                                new XYZ(min.X, min.Y, min.Z),
                                new XYZ(min.X, min.Y, max.Z),
                                new XYZ(min.X, max.Y, min.Z),
                                new XYZ(min.X, max.Y, max.Z),
                                max,
                                new XYZ(max.X, max.Y, min.Z),
                                new XYZ(max.X, min.Y, max.Z),
                                new XYZ(max.X, min.Y, min.Z)
                            };

                            List<double> bbParams = new List<double>();
                            foreach (XYZ corner in corners)
                            {
                                XYZ ptInHost = trans.IsIdentity ? corner : trans.OfPoint(corner);
                                IntersectionResult proj = mepLine.Project(ptInHost);
                                if (proj != null)
                                {
                                    bbParams.Add(proj.Parameter);
                                }
                            }

                            if (bbParams.Count > 0)
                            {
                                intervals.Add(new ClashInterval { Start = bbParams.Min(), End = bbParams.Max() });
                            }
                        }
                    }
                    catch { }
                }
            }

            return intervals;
        }
    }

    /// <summary>
    /// Обертка для хранения элементов из связанных файлов с их трансформацией.
    /// </summary>
    public class ObstacleInfo
    {
        public Element Element { get; }
        public Transform Transform { get; }

        public ObstacleInfo(Element elem, Transform transform)
        {
            Element = elem;
            Transform = transform ?? Transform.Identity;
        }
    }

    /// <summary>
    /// Фильтр выбора только для воздуховодов и трубопроводов в Revit.
    /// </summary>
    public class MepElementSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Duct || elem is Pipe;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
