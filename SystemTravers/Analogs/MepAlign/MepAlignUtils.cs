using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI.Selection;

namespace BimboClub.Analogs.MepAlign
{
    public static class MepAlignUtils
    {
        public const double Tolerance = 1e-6;
        public const double MmToFeet = 1.0 / 304.8;
        public const double FeetToMm = 304.8;

        public class MepSelectionFilter : ISelectionFilter
        {
            private readonly bool _onlyCurves;

            public MepSelectionFilter(bool onlyCurves = false)
            {
                _onlyCurves = onlyCurves;
            }

            public bool AllowElement(Element elem)
            {
                if (elem == null) return false;
                if (elem is MEPCurve) return true;
                if (!_onlyCurves && elem is FamilyInstance fi && fi.MEPModel != null)
                {
                    var connectors = fi.MEPModel.ConnectorManager?.Connectors;
                    return connectors != null && connectors.Size > 0;
                }
                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }

        public class MepAxisInfo
        {
            public Element Element { get; set; }
            public Line AxisLine { get; set; }
            public XYZ Origin { get; set; }
            public XYZ Direction { get; set; }
            public Connector SelectedConnector { get; set; }
            public bool IsVertical { get; set; }
            public bool IsCurve { get; set; }
        }

        /// <summary>
        /// Извлекает ось MEP-элемента (для кривой - осевая линия, для фитинга - ось ближайшего к точке клика коннектора).
        /// </summary>
        public static MepAxisInfo GetAxisInfo(Element elem, XYZ pickPoint)
        {
            if (elem == null) return null;

            if (elem is MEPCurve mepCurve)
            {
                if (mepCurve.Location is LocationCurve locCurve && locCurve.Curve is Line line)
                {
                    XYZ dir = line.Direction.Normalize();
                    bool isVert = Math.Abs(Math.Abs(dir.Z) - 1.0) < 0.05;
                    return new MepAxisInfo
                    {
                        Element = elem,
                        AxisLine = line,
                        Origin = line.GetEndPoint(0),
                        Direction = dir,
                        IsVertical = isVert,
                        IsCurve = true
                    };
                }
                else if (mepCurve.Location is LocationCurve locC)
                {
                    XYZ p0 = locC.Curve.GetEndPoint(0);
                    XYZ p1 = locC.Curve.GetEndPoint(1);
                    XYZ dir = (p1 - p0).Normalize();
                    bool isVert = Math.Abs(Math.Abs(dir.Z) - 1.0) < 0.05;
                    return new MepAxisInfo
                    {
                        Element = elem,
                        AxisLine = Line.CreateBound(p0, p1),
                        Origin = p0,
                        Direction = dir,
                        IsVertical = isVert,
                        IsCurve = true
                    };
                }
            }
            else if (elem is FamilyInstance fi && fi.MEPModel != null)
            {
                var connectors = fi.MEPModel.ConnectorManager?.Connectors;
                if (connectors != null && connectors.Size > 0)
                {
                    Connector closest = null;
                    double minDist = double.MaxValue;
                    foreach (Connector c in connectors)
                    {
                        if (c == null || c.ConnectorType == ConnectorType.Logical) continue;
                        double d = pickPoint != null ? c.Origin.DistanceTo(pickPoint) : 0;
                        if (d < minDist)
                        {
                            minDist = d;
                            closest = c;
                        }
                    }

                    if (closest != null)
                    {
                        XYZ dir = closest.CoordinateSystem.BasisZ.Normalize();
                        bool isVert = Math.Abs(Math.Abs(dir.Z) - 1.0) < 0.05;
                        Line unboundLine = Line.CreateUnbound(closest.Origin, dir);
                        return new MepAxisInfo
                        {
                            Element = elem,
                            AxisLine = unboundLine,
                            Origin = closest.Origin,
                            Direction = dir,
                            SelectedConnector = closest,
                            IsVertical = isVert,
                            IsCurve = false
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Выравнивает целевой элемент относительно опорного по оси (соосность или пересечение с вертикалью).
        /// </summary>
        public static bool AlignElement(Document doc, Element refElem, XYZ refPickPoint, Element targetElem, XYZ targetPickPoint)
        {
            if (refElem == null || targetElem == null || refElem.Id == targetElem.Id) return false;

            MepAxisInfo refAxis = GetAxisInfo(refElem, refPickPoint);
            MepAxisInfo targetAxis = GetAxisInfo(targetElem, targetPickPoint);

            if (refAxis == null || targetAxis == null) return false;

            XYZ moveVector = XYZ.Zero;

            if (!refAxis.IsVertical && !targetAxis.IsVertical)
            {
                // Сценарий 1: Оба элемента не вертикальные (горизонтальные или наклонные)
                // Смещаем target перпендикулярно его оси так, чтобы его ось легла на линию ref
                XYZ pTarget = targetAxis.Origin;
                IntersectionResult proj = refAxis.AxisLine.Project(pTarget);
                if (proj == null)
                {
                    // Если линия ограниченная, проецируем на бесконечную прямую
                    Line unboundRef = Line.CreateUnbound(refAxis.Origin, refAxis.Direction);
                    proj = unboundRef.Project(pTarget);
                }

                if (proj != null)
                {
                    XYZ pOnRef = proj.XYZPoint;
                    XYZ delta = pOnRef - pTarget;

                    // Если прямые параллельны, delta - точный вектор совмещения осей
                    // Если не параллельны, смещаем в плоскости нормали к целевой кривой
                    XYZ targetDir = targetAxis.Direction;
                    XYZ alongTarget = targetDir.Multiply(delta.DotProduct(targetDir));
                    moveVector = delta - alongTarget; // Перпендикулярная составляющая сдвига
                }
            }
            else if (refAxis.IsVertical && !targetAxis.IsVertical)
            {
                // Сценарий 2: Опорный - вертикальный стояк, целевой - не вертикальный
                // Смещаем целевой в горизонтальной плоскости так, чтобы его ось прошла через ось стояка
                XYZ riserPt = refAxis.Origin; // Точка оси стояка
                XYZ targetPt = targetAxis.Origin;
                XYZ targetDir2D = new XYZ(targetAxis.Direction.X, targetAxis.Direction.Y, 0);

                if (targetDir2D.GetLength() < 1e-4) return false;
                targetDir2D = targetDir2D.Normalize();

                // Нормаль в горизонтальной плоскости к целевой линии
                XYZ normal2D = new XYZ(-targetDir2D.Y, targetDir2D.X, 0).Normalize();

                // Вектор от точки целевого к оси стояка в XY
                XYZ diff2D = new XYZ(riserPt.X - targetPt.X, riserPt.Y - targetPt.Y, 0);
                double offsetDist = diff2D.DotProduct(normal2D);

                moveVector = normal2D.Multiply(offsetDist);
            }
            else if (!refAxis.IsVertical && targetAxis.IsVertical)
            {
                // Сценарий 3: Опорный - не вертикальный, целевой - вертикальный стояк
                // Смещаем стояк в горизонтальной плоскости так, чтобы его ось легла на ось опорного элемента
                XYZ riserPt = targetAxis.Origin;
                Line unboundRef = Line.CreateUnbound(refAxis.Origin, refAxis.Direction);
                IntersectionResult proj = unboundRef.Project(riserPt);

                if (proj != null)
                {
                    XYZ pOnRef = proj.XYZPoint;
                    moveVector = new XYZ(pOnRef.X - riserPt.X, pOnRef.Y - riserPt.Y, 0);
                }
            }
            else
            {
                // Сценарий 4: Оба элемента вертикальные - совмещаем в XY
                moveVector = new XYZ(refAxis.Origin.X - targetAxis.Origin.X, refAxis.Origin.Y - targetAxis.Origin.Y, 0);
            }

            if (moveVector.GetLength() > 1e-5)
            {
                ElementTransformUtils.MoveElement(doc, targetElem.Id, moveVector);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Выравнивает целевой элемент по высоте (оси Z) до пересечения направлений с опорным элементом.
        /// </summary>
        public static bool AlignElevation(Document doc, Element refElem, XYZ refPickPoint, Element targetElem, XYZ targetPickPoint)
        {
            if (refElem == null || targetElem == null || refElem.Id == targetElem.Id) return false;

            MepAxisInfo refAxis = GetAxisInfo(refElem, refPickPoint);
            MepAxisInfo targetAxis = GetAxisInfo(targetElem, targetPickPoint);

            if (refAxis == null || targetAxis == null) return false;

            double deltaZ = 0;

            // Ищем пересечение их проекций на плоскость XY
            XYZ interPt2D = FindIntersection2D(
                refAxis.Origin, refAxis.Direction,
                targetAxis.Origin, targetAxis.Direction);

            if (interPt2D != null)
            {
                // Находим отметку Z опорного элемента в точке пересечения в плане
                double tRef = (refAxis.Direction.X * (interPt2D.X - refAxis.Origin.X) + refAxis.Direction.Y * (interPt2D.Y - refAxis.Origin.Y))
                              / (refAxis.Direction.X * refAxis.Direction.X + refAxis.Direction.Y * refAxis.Direction.Y);
                double zRef = refAxis.Origin.Z + tRef * refAxis.Direction.Z;

                // Находим отметку Z целевого элемента в точке пересечения в плане
                double tTarget = (targetAxis.Direction.X * (interPt2D.X - targetAxis.Origin.X) + targetAxis.Direction.Y * (interPt2D.Y - targetAxis.Origin.Y))
                                 / (targetAxis.Direction.X * targetAxis.Direction.X + targetAxis.Direction.Y * targetAxis.Direction.Y);
                double zTarget = targetAxis.Origin.Z + tTarget * targetAxis.Direction.Z;

                deltaZ = zRef - zTarget;
            }
            else
            {
                // Если проекции параллельны или не пересекаются, берем разницу между характерными точками
                deltaZ = refAxis.Origin.Z - targetAxis.Origin.Z;
            }

            if (Math.Abs(deltaZ) > 1e-5)
            {
                ElementTransformUtils.MoveElement(doc, targetElem.Id, new XYZ(0, 0, deltaZ));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Выравнивает стояк вертикально (верхний конец устанавливается точно над нижним).
        /// </summary>
        public static bool AlignVertical(Document doc, MEPCurve curve)
        {
            if (curve == null) return false;
            if (!(curve.Location is LocationCurve locCurve)) return false;

            XYZ p0 = locCurve.Curve.GetEndPoint(0);
            XYZ p1 = locCurve.Curve.GetEndPoint(1);

            XYZ bottom = p0.Z < p1.Z ? p0 : p1;
            XYZ top = p0.Z < p1.Z ? p1 : p0;

            XYZ newTop = new XYZ(bottom.X, bottom.Y, top.Z);

            if (newTop.DistanceTo(top) > 1e-5)
            {
                locCurve.Curve = Line.CreateBound(bottom, newTop);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Получает полуширину/радиус MEP-элемента в направлении смещения.
        /// </summary>
        public static double GetHalfDimension(Element elem, bool isVerticalDirection)
        {
            if (elem == null) return 0;

            // 1. Трубы и круглые воздуховоды
            Parameter dParam = elem.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)
                               ?? elem.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            if (dParam != null && dParam.HasValue)
            {
                return dParam.AsDouble() / 2.0;
            }

            // 2. Прямоугольные/овальные воздуховоды и лотки
            if (isVerticalDirection)
            {
                Parameter hParam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)
                                   ?? elem.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                if (hParam != null && hParam.HasValue) return hParam.AsDouble() / 2.0;
            }
            else
            {
                Parameter wParam = elem.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)
                                   ?? elem.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                if (wParam != null && wParam.HasValue) return wParam.AsDouble() / 2.0;
            }

            // Фоллбэк по габаритам BoundingBox
            BoundingBoxXYZ bb = elem.get_BoundingBox(null);
            if (bb != null)
            {
                if (isVerticalDirection) return (bb.Max.Z - bb.Min.Z) / 2.0;
                return Math.Min(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y) / 2.0;
            }

            return 0;
        }

        /// <summary>
        /// Возвращает толщину изоляции элемента в футах.
        /// </summary>
        public static double GetInsulationThickness(Document doc, Element elem)
        {
            if (elem == null) return 0;

            try
            {
                var insIds = InsulationLiningBase.GetInsulationIds(doc, elem.Id);
                if (insIds != null && insIds.Count > 0)
                {
                    Element insElem = doc.GetElement(insIds.First());
                    Parameter thickParam = insElem?.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS)
                                           ?? insElem?.LookupParameter("Толщина изоляции")
                                           ?? insElem?.LookupParameter("Insulation Thickness");
                    if (thickParam != null && thickParam.HasValue)
                    {
                        return thickParam.AsDouble();
                    }
                }
            }
            catch { }

            Parameter insParam = elem.LookupParameter("Толщина изоляции")
                                 ?? elem.LookupParameter("Insulation Thickness");
            if (insParam != null && insParam.HasValue && insParam.AsDouble() > 1e-5)
            {
                return insParam.AsDouble();
            }

            return 0;
        }

        private static XYZ FindIntersection2D(XYZ p1, XYZ v1, XYZ p2, XYZ v2)
        {
            double dx1 = v1.X, dy1 = v1.Y;
            double dx2 = v2.X, dy2 = v2.Y;

            double det = dx1 * (-dy2) - dy1 * (-dx2);
            if (Math.Abs(det) < 1e-6) return null; // Параллельны в 2D

            double b1 = (p2.X - p1.X);
            double b2 = (p2.Y - p1.Y);

            double t1 = (b1 * (-dy2) - b2 * (-dx2)) / det;

            return new XYZ(p1.X + t1 * dx1, p1.Y + t1 * dy1, 0);
        }
    }
}
