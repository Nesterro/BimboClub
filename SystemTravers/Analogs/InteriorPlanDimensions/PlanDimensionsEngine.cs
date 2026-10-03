using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Analogs.InteriorPlanDimensions
{
    public class PlanDimensionsOptions
    {
        public DimensionType SelectedDimType { get; set; }
        public double OffsetMm { get; set; } = 800.0; // Отступ размерной линии в мм
        public bool DimensionOpenings { get; set; } = true;
        public bool DimensionGrids { get; set; } = true;
        public bool DimensionWallThickness { get; set; } = true;
        public bool DimensionColumns { get; set; } = true;
        public bool ColumnGridsTie { get; set; } = true;
        public double MinWallThicknessMm { get; set; } = 50.0;
        public string ExcludeTypeNames { get; set; } = "";
    }

    public class PlanDimensionsResult
    {
        public int DimensionsCreated { get; set; }
        public int WallsProcessed { get; set; }
        public int ColumnsProcessed { get; set; }
        public List<string> Messages { get; } = new List<string>();
    }

    public static class PlanDimensionsEngine
    {
        private const double FeetToMm = 304.8;
        private const double MmToFeet = 1.0 / 304.8;

        public static PlanDimensionsResult CreateDimensions(
            Document doc,
            ViewPlan planView,
            List<Element> elements,
            PlanDimensionsOptions options)
        {
            var result = new PlanDimensionsResult();
            if (doc == null || planView == null || options == null) return result;

            DimensionType dimType = options.SelectedDimType ?? GetDefaultDimensionType(doc);
            if (dimType == null)
            {
                result.Messages.Add("Не найден тип линейных размеров в проекте.");
                return result;
            }

            // Получаем все оси текущего вида
            List<Grid> grids = new FilteredElementCollector(doc, planView.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Where(g => g.Curve is Line)
                .ToList();

            using (Transaction tr = new Transaction(doc, "Автоматическая простановка размеров"))
            {
                tr.Start();

                var walls = elements.OfType<Wall>().ToList();
                var columns = elements.OfType<FamilyInstance>()
                    .Where(fi => fi.Category != null && (
                        fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Columns ||
                        fi.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns))
                    .ToList();

                // 1. Обработка стен
                foreach (Wall wall in walls)
                {
                    try
                    {
                        if (ProcessWall(doc, planView, wall, grids, dimType, options, result))
                        {
                            result.WallsProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"Ошибка обработки стены {wall.Id}: {ex.Message}");
                    }
                }

                // 2. Обработка колонн
                if (options.DimensionColumns)
                {
                    foreach (FamilyInstance col in columns)
                    {
                        try
                        {
                            if (ProcessColumn(doc, planView, col, grids, dimType, options, result))
                            {
                                result.ColumnsProcessed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Messages.Add($"Ошибка обработки колонны {col.Id}: {ex.Message}");
                        }
                    }
                }

                tr.Commit();
            }

            return result;
        }

        private static bool ProcessWall(
            Document doc,
            ViewPlan planView,
            Wall wall,
            List<Grid> grids,
            DimensionType dimType,
            PlanDimensionsOptions options,
            PlanDimensionsResult result)
        {
            if (!(wall.Location is LocationCurve locCurve) || !(locCurve.Curve is Line centerLine))
                return false;

            // Проверка минимальной толщины
            double wallWidthMm = wall.Width * FeetToMm;
            if (wallWidthMm < options.MinWallThicknessMm)
                return false;

            // Проверка имени типа на исключения
            if (!string.IsNullOrWhiteSpace(options.ExcludeTypeNames))
            {
                string typeName = wall.WallType?.Name ?? "";
                var filters = options.ExcludeTypeNames.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var f in filters)
                {
                    if (typeName.IndexOf(f.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        return false;
                }
            }

            XYZ p0 = centerLine.GetEndPoint(0);
            XYZ p1 = centerLine.GetEndPoint(1);
            XYZ wallDir = (p1 - p0).Normalize();
            XYZ normalDir = new XYZ(-wallDir.Y, wallDir.X, 0).Normalize();

            Options geomOpt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = planView
            };

            GeometryElement geomEl = wall.get_Geometry(geomOpt);
            if (geomEl == null) return false;

            // Сбор плоских граней стены
            List<PlanarFace> endFaces = new List<PlanarFace>(); // нормаль параллельна стене
            List<PlanarFace> sideFaces = new List<PlanarFace>(); // нормаль перпендикулярна стене

            foreach (GeometryObject obj in geomEl)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face f in solid.Faces)
                    {
                        if (f is PlanarFace pf && pf.Reference != null)
                        {
                            double dot = Math.Abs(pf.FaceNormal.DotProduct(XYZ.BasisZ));
                            if (dot > 0.5) continue; // горизонтальная грань (верх/низ), пропускаем

                            double dotWall = Math.Abs(pf.FaceNormal.DotProduct(wallDir));
                            if (dotWall > 0.85)
                            {
                                endFaces.Add(pf);
                            }
                            else if (dotWall < 0.15)
                            {
                                sideFaces.Add(pf);
                            }
                        }
                    }
                }
            }

            double offsetFt = options.OffsetMm * MmToFeet;
            double zElev = p0.Z;

            // --- А: Продольная размерная цепочка по стене и проемам ---
            if (endFaces.Count >= 2)
            {
                // Сортируем торцевые грани и грани проемов вдоль направления стены
                var sortedFaceRefs = new List<Tuple<double, Reference>>();

                foreach (var face in endFaces)
                {
                    XYZ orig = face.Origin;
                    double dist = (orig - p0).DotProduct(wallDir);

                    // Проверяем уникальность расстояния (чтобы не дублировать противоположные грани одного среза)
                    if (!sortedFaceRefs.Any(r => Math.Abs(r.Item1 - dist) < 0.05)) // 15 мм допуск
                    {
                        sortedFaceRefs.Add(new Tuple<double, Reference>(dist, face.Reference));
                    }
                }

                // Добавляем параллельные оси
                if (options.DimensionGrids)
                {
                    foreach (var g in grids)
                    {
                        if (g.Curve is Line gLine)
                        {
                            XYZ gDir = (gLine.GetEndPoint(1) - gLine.GetEndPoint(0)).Normalize();
                            // Ось должна быть перпендикулярна стене
                            if (Math.Abs(gDir.DotProduct(wallDir)) < 0.15)
                            {
                                IntersectionResult ir = gLine.Project(p0);
                                if (ir != null)
                                {
                                    XYZ projPt = ir.XYZPoint;
                                    double dist = (projPt - p0).DotProduct(wallDir);

                                    // Проверяем, находится ли ось в пределах стены или близко к ней
                                    double minD = sortedFaceRefs.Min(r => r.Item1) - 3.0; // до 1 метра от торца
                                    double maxD = sortedFaceRefs.Max(r => r.Item1) + 3.0;
                                    if (dist >= minD && dist <= maxD)
                                    {
                                        if (!sortedFaceRefs.Any(r => Math.Abs(r.Item1 - dist) < 0.05))
                                        {
                                            sortedFaceRefs.Add(new Tuple<double, Reference>(dist, new Reference(g)));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                sortedFaceRefs.Sort((a, b) => a.Item1.CompareTo(b.Item1));

                if (sortedFaceRefs.Count >= 2)
                {
                    ReferenceArray refArray = new ReferenceArray();
                    foreach (var tuple in sortedFaceRefs)
                    {
                        refArray.Append(tuple.Item2);
                    }

                    XYZ lineStart = (p0 + wallDir * sortedFaceRefs.First().Item1) + normalDir * (wall.Width * 0.5 + offsetFt);
                    XYZ lineEnd = (p0 + wallDir * sortedFaceRefs.Last().Item1) + normalDir * (wall.Width * 0.5 + offsetFt);
                    lineStart = new XYZ(lineStart.X, lineStart.Y, zElev);
                    lineEnd = new XYZ(lineEnd.X, lineEnd.Y, zElev);

                    if (lineStart.DistanceTo(lineEnd) > 0.01)
                    {
                        Line dimLine = Line.CreateBound(lineStart, lineEnd);
                        Dimension dim = doc.Create.NewDimension(planView, dimLine, refArray, dimType);
                        if (dim != null) result.DimensionsCreated++;
                    }
                }
            }

            // --- Б: Поперечный размер (толщина стены) ---
            if (options.DimensionWallThickness && sideFaces.Count >= 2)
            {
                var sideA = sideFaces.FirstOrDefault(f => f.FaceNormal.DotProduct(normalDir) > 0.7);
                var sideB = sideFaces.FirstOrDefault(f => f.FaceNormal.DotProduct(normalDir) < -0.7);

                if (sideA != null && sideB != null && sideA.Reference != null && sideB.Reference != null)
                {
                    ReferenceArray thickRefs = new ReferenceArray();
                    thickRefs.Append(sideA.Reference);
                    thickRefs.Append(sideB.Reference);

                    XYZ midPt = (p0 + p1) * 0.5;
                    XYZ dimStart = midPt - normalDir * (wall.Width * 0.5 + 1.0);
                    XYZ dimEnd = midPt + normalDir * (wall.Width * 0.5 + 1.0);
                    dimStart = new XYZ(dimStart.X, dimStart.Y, zElev);
                    dimEnd = new XYZ(dimEnd.X, dimEnd.Y, zElev);

                    Line thickLine = Line.CreateBound(dimStart, dimEnd);
                    Dimension thickDim = doc.Create.NewDimension(planView, thickLine, thickRefs, dimType);
                    if (thickDim != null) result.DimensionsCreated++;
                }
            }

            return true;
        }

        private static bool ProcessColumn(
            Document doc,
            ViewPlan planView,
            FamilyInstance col,
            List<Grid> grids,
            DimensionType dimType,
            PlanDimensionsOptions options,
            PlanDimensionsResult result)
        {
            LocationPoint locPt = col.Location as LocationPoint;
            if (locPt == null) return false;

            XYZ center = locPt.Point;
            double zElev = center.Z;

            Options geomOpt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = planView
            };

            GeometryElement geomEl = col.get_Geometry(geomOpt);
            if (geomEl == null) return false;

            List<PlanarFace> vertFaces = new List<PlanarFace>();
            foreach (GeometryObject obj in geomEl)
            {
                if (obj is Solid s && s.Faces.Size > 0)
                {
                    foreach (Face f in s.Faces)
                    {
                        if (f is PlanarFace pf && pf.Reference != null)
                        {
                            if (Math.Abs(pf.FaceNormal.DotProduct(XYZ.BasisZ)) < 0.2)
                            {
                                vertFaces.Add(pf);
                            }
                        }
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    GeometryElement instGeom = gi.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Solid s2 && s2.Faces.Size > 0)
                            {
                                foreach (Face f in s2.Faces)
                                {
                                    if (f is PlanarFace pf && pf.Reference != null)
                                    {
                                        if (Math.Abs(pf.FaceNormal.DotProduct(XYZ.BasisZ)) < 0.2)
                                        {
                                            vertFaces.Add(pf);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (vertFaces.Count < 2) return false;

            double offsetFt = options.OffsetMm * MmToFeet;

            // Группировка граней по направлению нормалей
            // Направление 1: X (или параллельно первым противоположным граням)
            var face1 = vertFaces[0];
            XYZ norm1 = face1.FaceNormal.Normalize();
            var opp1 = vertFaces.FirstOrDefault(f => f.FaceNormal.DotProduct(norm1) < -0.85);

            if (opp1 != null && face1.Reference != null && opp1.Reference != null)
            {
                ReferenceArray colRefs1 = new ReferenceArray();
                colRefs1.Append(face1.Reference);

                // Привязка к ближайшей параллельной оси
                if (options.ColumnGridsTie)
                {
                    Grid nearGrid = FindNearestParallelGrid(grids, norm1, center, 10.0);
                    if (nearGrid != null)
                    {
                        colRefs1.Append(new Reference(nearGrid));
                    }
                }

                colRefs1.Append(opp1.Reference);

                XYZ dirLine = new XYZ(-norm1.Y, norm1.X, 0).Normalize();
                XYZ pStart = (center - norm1 * 2.0) + dirLine * offsetFt;
                XYZ pEnd = (center + norm1 * 2.0) + dirLine * offsetFt;
                pStart = new XYZ(pStart.X, pStart.Y, zElev);
                pEnd = new XYZ(pEnd.X, pEnd.Y, zElev);

                Line dLine1 = Line.CreateBound(pStart, pEnd);
                Dimension d1 = doc.Create.NewDimension(planView, dLine1, colRefs1, dimType);
                if (d1 != null) result.DimensionsCreated++;
            }

            // Направление 2: Y (перпендикулярное первому)
            XYZ norm2 = new XYZ(-norm1.Y, norm1.X, 0).Normalize();
            var face2 = vertFaces.FirstOrDefault(f => Math.Abs(f.FaceNormal.DotProduct(norm2)) > 0.85);
            if (face2 != null)
            {
                XYZ norm2Real = face2.FaceNormal.Normalize();
                var opp2 = vertFaces.FirstOrDefault(f => f.FaceNormal.DotProduct(norm2Real) < -0.85);
                if (opp2 != null && face2.Reference != null && opp2.Reference != null)
                {
                    ReferenceArray colRefs2 = new ReferenceArray();
                    colRefs2.Append(face2.Reference);

                    if (options.ColumnGridsTie)
                    {
                        Grid nearGrid2 = FindNearestParallelGrid(grids, norm2Real, center, 10.0);
                        if (nearGrid2 != null)
                        {
                            colRefs2.Append(new Reference(nearGrid2));
                        }
                    }

                    colRefs2.Append(opp2.Reference);

                    XYZ dirLine2 = new XYZ(-norm2Real.Y, norm2Real.X, 0).Normalize();
                    XYZ pStart2 = (center - norm2Real * 2.0) + dirLine2 * offsetFt;
                    XYZ pEnd2 = (center + norm2Real * 2.0) + dirLine2 * offsetFt;
                    pStart2 = new XYZ(pStart2.X, pStart2.Y, zElev);
                    pEnd2 = new XYZ(pEnd2.X, pEnd2.Y, zElev);

                    Line dLine2 = Line.CreateBound(pStart2, pEnd2);
                    Dimension d2 = doc.Create.NewDimension(planView, dLine2, colRefs2, dimType);
                    if (d2 != null) result.DimensionsCreated++;
                }
            }

            return true;
        }

        private static Grid FindNearestParallelGrid(List<Grid> grids, XYZ norm, XYZ center, double maxDistanceFt)
        {
            Grid bestGrid = null;
            double bestDist = double.MaxValue;

            foreach (var g in grids)
            {
                if (g.Curve is Line gLine)
                {
                    XYZ gDir = (gLine.GetEndPoint(1) - gLine.GetEndPoint(0)).Normalize();
                    // Линия оси должна быть перпендикулярна norm (т.е. параллельна плоскости грани)
                    if (Math.Abs(gDir.DotProduct(norm)) < 0.15)
                    {
                        IntersectionResult ir = gLine.Project(center);
                        if (ir != null)
                        {
                            double dist = ir.Distance;
                            if (dist < bestDist && dist <= maxDistanceFt)
                            {
                                bestDist = dist;
                                bestGrid = g;
                            }
                        }
                    }
                }
            }

            return bestGrid;
        }

        public static DimensionType GetDefaultDimensionType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .FirstOrDefault(dt => dt.StyleType == DimensionStyleType.Linear);
        }
    }
}
