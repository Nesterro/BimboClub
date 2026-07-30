using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using BimboClub;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Transform = Autodesk.Revit.DB.Transform;
using Color = System.Windows.Media.Color;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using RowDefinition = System.Windows.Controls.RowDefinition;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using Border = System.Windows.Controls.Border;
using ControlTemplate = System.Windows.Controls.ControlTemplate;
using ContentPresenter = System.Windows.Controls.ContentPresenter;
using Binding = System.Windows.Data.Binding;
using RelativeSource = System.Windows.Data.RelativeSource;
using Cursors = System.Windows.Input.Cursors;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace DuctWallOpenings
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DuctWallOpeningsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OpeningsRunner.Run(commandData, ref message, OpeningsRunner.HostKind.Wall);
        }

        public static List<string> GetEditableDoubleParamNames(Document doc, FamilySymbol symbol)
        {
            return OpeningsRunner.GetEditableDoubleParamNames(doc, symbol);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DuctFloorOpeningsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OpeningsRunner.Run(commandData, ref message, OpeningsRunner.HostKind.Floor);
        }
    }

    public static class OpeningsRunner
    {
        public enum HostKind { Wall, Floor }

        public class MepInfo
        {
            public MEPCurve Element;
            public Transform Transform;
        }

        public static Result Run(ExternalCommandData commandData, ref string message, HostKind kind)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            List<string> errorLog = new List<string>();

            try
            {
                List<FamilySymbol> allSymbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => s != null && s.Family != null)
                    .OrderBy(s => s.Family.Name)
                    .ThenBy(s => s.Name)
                    .ToList();

                if (allSymbols.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "В проекте нет загруженных семейств.");
                    return Result.Failed;
                }

                bool hasPreselection = false;
                List<MEPCurve> preselectedMep = GetSelectedMepCurves(uiDoc, out hasPreselection);

                string titleSuffix = kind == HostKind.Wall ? "в стенах" : "в полах/перекрытиях";
                FamilySelectionWindow window = new FamilySelectionWindow(
                    doc, allSymbols, hasPreselection, titleSuffix);

                bool? result = window.ShowDialog();
                if (result != true || window.SelectedSymbol == null)
                    return Result.Cancelled;

                FamilySymbol openingSymbol = window.SelectedSymbol;
                double offsetMm = window.OffsetMm;
                double depthOffsetMm = window.DepthOffsetMm;
                string widthParamName = window.WidthParam;
                string heightParamName = window.HeightParam;
                string depthParamName = window.DepthParam;
                bool useSelectedOnly = window.UseSelectedOnly;
                bool useCurrentViewOnly = window.UseCurrentViewOnly;
                bool useDucts = window.UseDucts;
                bool usePipes = window.UsePipes;
                bool useTrays = window.UseTrays;

                if (!useDucts && !usePipes && !useTrays)
                {
                    TaskDialog.Show("Информация", "Не выбрано ни одного типа элементов (воздуховоды/трубы/лотки).");
                    return Result.Cancelled;
                }

                // Сбор MEP-элементов
                List<MepInfo> mepElements = CollectMepCurves(
                    doc, useDucts, usePipes, useTrays,
                    useSelectedOnly && hasPreselection, useCurrentViewOnly, preselectedMep);

                if (mepElements.Count == 0)
                {
                    string msg = useSelectedOnly ? "Среди выбранных нет подходящих элементов."
                              : useCurrentViewOnly ? "На текущем виде нет подходящих элементов."
                              : "В модели нет подходящих элементов.";
                    TaskDialog.Show("Информация", msg);
                    return Result.Cancelled;
                }

                List<HostInfo> hosts = kind == HostKind.Wall
                    ? CollectWalls(doc)
                    : CollectFloors(doc);

                if (hosts.Count == 0)
                {
                    TaskDialog.Show("Информация",
                        kind == HostKind.Wall
                            ? "Не найдено стен (включая связанные модели)."
                            : "Не найдено полов/перекрытий (включая связанные модели).");
                    return Result.Cancelled;
                }

                int createdCount = 0;
                int skippedCount = 0;
                double offset = UnitUtils.ConvertToInternalUnits(offsetMm, UnitTypeId.Millimeters);
                double depthOffset = UnitUtils.ConvertToInternalUnits(depthOffsetMm, UnitTypeId.Millimeters);

                using (Transaction t = new Transaction(doc, "Расстановка отверстий"))
                {
                    t.Start();

                    if (!openingSymbol.IsActive)
                    {
                        openingSymbol.Activate();
                        doc.Regenerate();
                    }

                    foreach (MepInfo mepInfo in mepElements)
                    {
                        try
                        {
                            if (mepInfo == null || mepInfo.Element == null) continue;
                            MEPCurve mep = mepInfo.Element;
                            Transform trMep = mepInfo.Transform ?? Transform.Identity;

                            LocationCurve lc = mep.Location as LocationCurve;
                            if (lc == null || lc.Curve == null) continue;

                            Curve mepCurve = lc.Curve;
                            if (trMep != null && !trMep.IsIdentity)
                            {
                                mepCurve = mepCurve.CreateTransformed(trMep);
                            }
                            Line mepLine = mepCurve as Line;
                            if (mepLine == null) continue;

                            XYZ mepStart = mepLine.GetEndPoint(0);
                            XYZ mepEnd = mepLine.GetEndPoint(1);
                            XYZ mepDir = (mepEnd - mepStart).Normalize();

                            bool isVertical = Math.Abs(mepDir.Z) > 0.7071;
                            if (kind == HostKind.Wall && isVertical) continue;
                            if (kind == HostKind.Floor && !isVertical) continue;

                            GetMepDimensions(mep, out double width, out double height, out double diameter);

                            double openingWidth, openingHeight;
                            if (diameter > 0)
                            {
                                openingWidth = diameter + 2 * offset;
                                openingHeight = diameter + 2 * offset;
                            }
                            else
                            {
                                openingWidth = width + 2 * offset;
                                openingHeight = height + 2 * offset;
                            }

                            foreach (HostInfo hostInfo in hosts)
                            {
                                try
                                {
                                    XYZ intersectPoint = null;
                                    double hostThickness = 0;

                                    if (kind == HostKind.Wall)
                                    {
                                        Wall wall = hostInfo.Element as Wall;
                                        if (wall == null) continue;
                                        Transform tr = hostInfo.Transform ?? Transform.Identity;

                                        LocationCurve wallLc = wall.Location as LocationCurve;
                                        if (wallLc == null || wallLc.Curve == null) continue;
                                        Line wallLineLocal = wallLc.Curve as Line;
                                        if (wallLineLocal == null) continue;

                                        Curve wallCurve = wallLineLocal.CreateTransformed(tr);
                                        Line wallLine = wallCurve as Line;
                                        if (wallLine == null) continue;

                                        if (!IsMepInsideWallHeight(mepLine, wall, tr)) continue;

                                        intersectPoint = GetWallIntersection(mepLine, wallLine);
                                        if (intersectPoint == null) continue;

                                        try { hostThickness = wall.Width; } catch { }
                                    }
                                    else
                                    {
                                        Floor floor = hostInfo.Element as Floor;
                                        if (floor == null) continue;
                                        Transform tr = hostInfo.Transform ?? Transform.Identity;

                                        intersectPoint = GetFloorIntersection(mepLine, floor, tr, out hostThickness);
                                        if (intersectPoint == null) continue;
                                    }

                                    FamilyInstance opening = doc.Create.NewFamilyInstance(
                                        intersectPoint,
                                        openingSymbol,
                                        StructuralType.NonStructural);
                                    if (opening == null) { skippedCount++; continue; }

                                    doc.Regenerate();

                                    if (kind == HostKind.Wall)
                                    {
                                        try
                                        {
                                            XYZ mepDir2D = new XYZ(mepDir.X, mepDir.Y, 0);
                                            mepDir2D = mepDir2D.GetLength() > 1e-9 ? mepDir2D.Normalize() : XYZ.BasisX;

                                            XYZ currentFacing = opening.FacingOrientation ?? XYZ.BasisY;
                                            XYZ currentFacing2D = new XYZ(currentFacing.X, currentFacing.Y, 0);
                                            currentFacing2D = currentFacing2D.GetLength() > 1e-9
                                                ? currentFacing2D.Normalize()
                                                : XYZ.BasisY;

                                            double currentAngle = Math.Atan2(currentFacing2D.Y, currentFacing2D.X);
                                            double targetAngle = Math.Atan2(mepDir2D.Y, mepDir2D.X);
                                            double rotateAngle = targetAngle - currentAngle;

                                            if (Math.Abs(rotateAngle) > 1e-9)
                                            {
                                                Line axis = Line.CreateBound(intersectPoint, intersectPoint + XYZ.BasisZ);
                                                ElementTransformUtils.RotateElement(doc, opening.Id, axis, rotateAngle);
                                            }
                                        }
                                        catch (Exception exRot)
                                        {
                                            errorLog.Add("Поворот: " + exRot.Message);
                                        }
                                    }

                                    SetParam(opening, widthParamName, openingWidth);
                                    SetParam(opening, heightParamName, openingHeight);
                                    if (hostThickness > 0)
                                        SetParam(opening, depthParamName, hostThickness + depthOffset);

                                    createdCount++;
                                }
                                catch (Exception exInner)
                                {
                                    skippedCount++;
                                    errorLog.Add("Хост: " + exInner.Message);
                                }
                            }
                        }
                        catch (Exception exMep)
                        {
                            skippedCount++;
                            errorLog.Add("MEP: " + exMep.Message);
                        }
                    }

                    t.Commit();
                }

                string info = $"Создано отверстий: {createdCount}\nПропущено пересечений: {skippedCount}";
                List<string> modes = new List<string>();
                if (useDucts) modes.Add("воздуховоды");
                if (usePipes) modes.Add("трубы");
                if (useTrays) modes.Add("лотки");
                info = "Типы: " + string.Join(", ", modes) + "\n" + info;
                if (useSelectedOnly) info = "Режим: только выбранные\n" + info;
                else if (useCurrentViewOnly) info = "Режим: только текущий вид\n" + info;
                if (errorLog.Count > 0)
                {
                    int show = Math.Min(5, errorLog.Count);
                    info += "\n\nПервые ошибки:\n" + string.Join("\n", errorLog.Take(show));
                }
                TaskDialog.Show("Готово", info);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        public class HostInfo
        {
            public Element Element;
            public Transform Transform;
        }

        // ---- Сбор MEP-элементов ----
        private static List<MEPCurve> GetSelectedMepCurves(UIDocument uiDoc, out bool hasPreselection)
        {
            hasPreselection = false;
            var list = new List<MEPCurve>();
            try
            {
                ICollection<ElementId> ids = uiDoc.Selection.GetElementIds();
                if (ids == null || ids.Count == 0) return list;
                foreach (var id in ids)
                {
                    Element e = uiDoc.Document.GetElement(id);
                    if (e is Duct || e is Pipe || e is CableTray)
                        list.Add(e as MEPCurve);
                }
                hasPreselection = list.Count > 0;
            }
            catch { }
            return list;
        }

        private static bool IsMepInView(MEPCurve mep, View activeView, Transform tr)
        {
            try
            {
                BoundingBoxXYZ viewBB = activeView.get_BoundingBox(null);
                if (viewBB == null) return true;

                BoundingBoxXYZ mepBB = mep.get_BoundingBox(null);
                if (mepBB == null) return false;

                XYZ min = tr.OfPoint(mepBB.Min);
                XYZ max = tr.OfPoint(mepBB.Max);
                double mepMinX = Math.Min(min.X, max.X);
                double mepMaxX = Math.Max(min.X, max.X);
                double mepMinY = Math.Min(min.Y, max.Y);
                double mepMaxY = Math.Max(min.Y, max.Y);
                double mepMinZ = Math.Min(min.Z, max.Z);
                double mepMaxZ = Math.Max(min.Z, max.Z);

                if (mepMaxX < viewBB.Min.X || mepMinX > viewBB.Max.X) return false;
                if (mepMaxY < viewBB.Min.Y || mepMinY > viewBB.Max.Y) return false;
                if (mepMaxZ < viewBB.Min.Z || mepMinZ > viewBB.Max.Z) return false;

                return true;
            }
            catch { return true; }
        }

        private static List<MepInfo> CollectMepCurves(Document doc,
            bool useDucts, bool usePipes, bool useTrays,
            bool selectedOnly, bool currentViewOnly,
            List<MEPCurve> preselected)
        {
            var result = new List<MepInfo>();

            if (selectedOnly)
            {
                foreach (var m in preselected)
                {
                    if (m is Duct && useDucts) result.Add(new MepInfo { Element = m, Transform = Transform.Identity });
                    else if (m is Pipe && usePipes) result.Add(new MepInfo { Element = m, Transform = Transform.Identity });
                    else if (m is CableTray && useTrays) result.Add(new MepInfo { Element = m, Transform = Transform.Identity });
                }
                return result;
            }

            // 1. Из основного документа
            ElementId viewId = currentViewOnly ? doc.ActiveView.Id : null;

            if (useDucts)
            {
                var c = viewId != null
                    ? new FilteredElementCollector(doc, viewId)
                    : new FilteredElementCollector(doc);
                foreach (var x in c.OfClass(typeof(Duct)).Cast<MEPCurve>())
                    result.Add(new MepInfo { Element = x, Transform = Transform.Identity });
            }
            if (usePipes)
            {
                var c = viewId != null
                    ? new FilteredElementCollector(doc, viewId)
                    : new FilteredElementCollector(doc);
                foreach (var x in c.OfClass(typeof(Pipe)).Cast<MEPCurve>())
                    result.Add(new MepInfo { Element = x, Transform = Transform.Identity });
            }
            if (useTrays)
            {
                var c = viewId != null
                    ? new FilteredElementCollector(doc, viewId)
                    : new FilteredElementCollector(doc);
                foreach (var x in c.OfClass(typeof(CableTray)).Cast<MEPCurve>())
                    result.Add(new MepInfo { Element = x, Transform = Transform.Identity });
            }

            // 2. Из связанных документов
            foreach (var link in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                if (link == null) continue;
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;
                Transform tr = link.GetTotalTransform() ?? Transform.Identity;

                if (useDucts)
                {
                    var c = new FilteredElementCollector(linkDoc);
                    foreach (var x in c.OfClass(typeof(Duct)).Cast<MEPCurve>())
                    {
                        if (currentViewOnly && !IsMepInView(x, doc.ActiveView, tr)) continue;
                        result.Add(new MepInfo { Element = x, Transform = tr });
                    }
                }
                if (usePipes)
                {
                    var c = new FilteredElementCollector(linkDoc);
                    foreach (var x in c.OfClass(typeof(Pipe)).Cast<MEPCurve>())
                    {
                        if (currentViewOnly && !IsMepInView(x, doc.ActiveView, tr)) continue;
                        result.Add(new MepInfo { Element = x, Transform = tr });
                    }
                }
                if (useTrays)
                {
                    var c = new FilteredElementCollector(linkDoc);
                    foreach (var x in c.OfClass(typeof(CableTray)).Cast<MEPCurve>())
                    {
                        if (currentViewOnly && !IsMepInView(x, doc.ActiveView, tr)) continue;
                        result.Add(new MepInfo { Element = x, Transform = tr });
                    }
                }
            }

            return result;
        }

        // ---- Габариты MEP-элемента ----
        private static void GetMepDimensions(Element e, out double width, out double height, out double diameter)
        {
            width = 0; height = 0; diameter = 0;
            if (e is Duct)
            {
                width = GetParamValue(e, BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                height = GetParamValue(e, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                diameter = GetParamValue(e, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            }
            else if (e is Pipe pipe)
            {
                diameter = GetParamValue(e, BuiltInParameter.RBS_PIPE_OUTER_DIAMETER);
                if (diameter <= 0)
                    diameter = GetParamValue(e, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (diameter <= 0)
                    try { diameter = pipe.Diameter; } catch { }
            }
            else if (e is CableTray tray)
            {
                width = GetParamValue(e, BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                height = GetParamValue(e, BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                if (width <= 0) try { width = tray.Width; } catch { }
                if (height <= 0) try { height = tray.Height; } catch { }
            }
        }

        // ---- Стены и полы ----
        public static List<HostInfo> CollectWalls(Document doc)
        {
            var result = new List<HostInfo>();
            foreach (var w in new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>())
                if (w != null) result.Add(new HostInfo { Element = w, Transform = Transform.Identity });

            foreach (var link in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                if (link == null) continue;
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;
                Transform tr = link.GetTotalTransform() ?? Transform.Identity;
                foreach (var w in new FilteredElementCollector(linkDoc).OfClass(typeof(Wall)).Cast<Wall>())
                    if (w != null) result.Add(new HostInfo { Element = w, Transform = tr });
            }
            return result;
        }

        public static List<HostInfo> CollectFloors(Document doc)
        {
            var result = new List<HostInfo>();
            foreach (var f in new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>())
                if (f != null) result.Add(new HostInfo { Element = f, Transform = Transform.Identity });

            foreach (var link in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                if (link == null) continue;
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;
                Transform tr = link.GetTotalTransform() ?? Transform.Identity;
                foreach (var f in new FilteredElementCollector(linkDoc).OfClass(typeof(Floor)).Cast<Floor>())
                    if (f != null) result.Add(new HostInfo { Element = f, Transform = tr });
            }
            return result;
        }

        private static bool IsMepInsideWallHeight(Line mepLine, Wall wall, Transform tr)
        {
            try
            {
                BoundingBoxXYZ bb = wall.get_BoundingBox(null);
                if (bb == null) return true;
                XYZ min = tr.OfPoint(bb.Min);
                XYZ max = tr.OfPoint(bb.Max);
                double zmin = Math.Min(min.Z, max.Z);
                double zmax = Math.Max(min.Z, max.Z);

                double dz1 = mepLine.GetEndPoint(0).Z;
                double dz2 = mepLine.GetEndPoint(1).Z;
                double dzMin = Math.Min(dz1, dz2);
                double dzMax = Math.Max(dz1, dz2);

                return dzMax >= zmin - 0.01 && dzMin <= zmax + 0.01;
            }
            catch { return true; }
        }

        private static XYZ GetWallIntersection(Line mepLine, Line wallLine)
        {
            if (mepLine == null || wallLine == null) return null;

            XYZ ds = mepLine.GetEndPoint(0);
            XYZ de = mepLine.GetEndPoint(1);
            double z = (ds.Z + de.Z) / 2.0;

            XYZ ds2 = new XYZ(ds.X, ds.Y, z);
            XYZ de2 = new XYZ(de.X, de.Y, z);
            if (ds2.IsAlmostEqualTo(de2)) return null;
            Line mepLine2D = Line.CreateBound(ds2, de2);

            XYZ ws = wallLine.GetEndPoint(0);
            XYZ we = wallLine.GetEndPoint(1);
            XYZ ws2 = new XYZ(ws.X, ws.Y, z);
            XYZ we2 = new XYZ(we.X, we.Y, z);
            if (ws2.IsAlmostEqualTo(we2)) return null;

            XYZ wallDir = (we2 - ws2).Normalize();
            Line wallLine2D = Line.CreateBound(ws2 - wallDir * 0.001, we2 + wallDir * 0.001);

            IntersectionResultArray results = null;
            SetComparisonResult cmp;
            try { cmp = mepLine2D.Intersect(wallLine2D, out results); }
            catch { return null; }

            if (cmp != SetComparisonResult.Overlap || results == null || results.Size == 0)
                return null;

            XYZ point = results.get_Item(0).XYZPoint;
            if (point == null) return null;
            if (!IsPointOnSegment(point, ds2, de2)) return null;
            if (!IsPointOnSegment(point, ws2, we2)) return null;

            double len = mepLine.Length;
            if (len < 1e-9) return null;

            double param = mepLine2D.Project(point).Parameter;
            return mepLine.Evaluate(param / len, true);
        }

        private static XYZ GetFloorIntersection(Line mepLine, Floor floor, Transform tr, out double thickness)
        {
            thickness = 0;
            try
            {
                XYZ ds = mepLine.GetEndPoint(0);
                XYZ de = mepLine.GetEndPoint(1);
                double dzMin = Math.Min(ds.Z, de.Z);
                double dzMax = Math.Max(ds.Z, de.Z);

                BoundingBoxXYZ bb = floor.get_BoundingBox(null);
                if (bb == null) return null;
                XYZ min = tr.OfPoint(bb.Min);
                XYZ max = tr.OfPoint(bb.Max);
                double zTop = Math.Max(min.Z, max.Z);
                double zBot = Math.Min(min.Z, max.Z);

                thickness = Math.Abs(zTop - zBot);

                if (dzMax < zBot - 0.01 || dzMin > zTop + 0.01) return null;

                double x = (ds.X + de.X) / 2.0;
                double y = (ds.Y + de.Y) / 2.0;
                if (Math.Abs(ds.X - de.X) > 1e-6 || Math.Abs(ds.Y - de.Y) > 1e-6)
                {
                    double zMid = (zTop + zBot) / 2.0;
                    double dz = de.Z - ds.Z;
                    if (Math.Abs(dz) < 1e-9) return null;
                    double tParam = (zMid - ds.Z) / dz;
                    if (tParam < -0.01 || tParam > 1.01) return null;
                    x = ds.X + (de.X - ds.X) * tParam;
                    y = ds.Y + (de.Y - ds.Y) * tParam;
                }

                XYZ candidate = new XYZ(x, y, (zTop + zBot) / 2.0);
                if (!IsPointInsideFloor(floor, tr, candidate)) return null;
                return candidate;
            }
            catch { return null; }
        }

        private static bool IsPointInsideFloor(Floor floor, Transform tr, XYZ point)
        {
            try
            {
                Options opt = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false };
                GeometryElement ge = floor.get_Geometry(opt);
                if (ge == null) return true;

                foreach (GeometryObject go in ge)
                {
                    Solid s = go as Solid;
                    if (s == null || s.Volume < 1e-9) continue;
                    XYZ localPoint = tr.Inverse.OfPoint(point);

                    foreach (Face face in s.Faces)
                    {
                        PlanarFace pf = face as PlanarFace;
                        if (pf == null) continue;
                        if (Math.Abs(pf.FaceNormal.Z) < 0.9) continue;

                        IntersectionResult ir = face.Project(new XYZ(localPoint.X, localPoint.Y, pf.Origin.Z));
                        if (ir != null && ir.Distance < 0.001) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsPointOnSegment(XYZ p, XYZ a, XYZ b, double tol = 0.001)
        {
            double d1 = p.DistanceTo(a);
            double d2 = p.DistanceTo(b);
            double full = a.DistanceTo(b);
            return Math.Abs(d1 + d2 - full) < tol;
        }

        private static double GetParamValue(Element e, BuiltInParameter bip)
        {
            if (e == null) return 0;
            Parameter p = e.get_Parameter(bip);
            if (p == null || !p.HasValue) return 0;
            return p.AsDouble();
        }

        private static void SetParam(FamilyInstance inst, string paramName, double value)
        {
            if (inst == null || string.IsNullOrEmpty(paramName)) return;
            try
            {
                Parameter p = inst.LookupParameter(paramName);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                    p.Set(value);
            }
            catch { }
        }

        public static List<string> GetEditableDoubleParamNames(Document doc, FamilySymbol symbol)
        {
            var names = new HashSet<string>();
            if (symbol == null) return new List<string>();

            try
            {
                foreach (Parameter p in symbol.Parameters)
                {
                    if (p == null || p.Definition == null) continue;
                    if (p.StorageType == StorageType.Double && !p.IsReadOnly)
                        names.Add(p.Definition.Name);
                }
            }
            catch { }

            try
            {
                FamilyInstance any = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .FirstOrDefault(fi => fi != null && fi.Symbol != null && fi.Symbol.Id == symbol.Id);

                if (any != null)
                {
                    foreach (Parameter p in any.Parameters)
                    {
                        if (p == null || p.Definition == null) continue;
                        if (p.StorageType == StorageType.Double && !p.IsReadOnly)
                            names.Add(p.Definition.Name);
                    }
                }
                else
                {
                    using (Transaction tr = new Transaction(doc, "Чтение параметров"))
                    {
                        tr.Start();
                        try
                        {
                            if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
                            FamilyInstance temp = doc.Create.NewFamilyInstance(
                                XYZ.Zero, symbol, StructuralType.NonStructural);
                            doc.Regenerate();
                            if (temp != null)
                            {
                                foreach (Parameter p in temp.Parameters)
                                {
                                    if (p == null || p.Definition == null) continue;
                                    if (p.StorageType == StorageType.Double && !p.IsReadOnly)
                                        names.Add(p.Definition.Name);
                                }
                            }
                        }
                        catch { }
                        tr.RollBack();
                    }
                }
            }
            catch { }

            return names.OrderBy(n => n).ToList();
        }

        // ========================================================================
        // Объединение отверстий - Бизнес-логика
        // ========================================================================
        public static Result RunMerge(ExternalCommandData commandData, ref string message, OpeningsRunner.HostKind kind)
        {
            Logger.Log(string.Format("Запуск объединения отверстий: {0}.", kind), "INFO");
            UIApplication application = commandData.Application;
            UIDocument activeUIDocument = application.ActiveUIDocument;
            Document document = activeUIDocument.Document;
            
            try
            {
                List<FamilySymbol> list = new FilteredElementCollector(document)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => s != null && s.Family != null)
                    .OrderBy(s => s.Family.Name)
                    .ThenBy(s => s.Name)
                    .ToList();

                if (list.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "В проекте нет загруженных семейств.");
                    return Result.Failed;
                }

                string titleSuffix = (kind == OpeningsRunner.HostKind.Wall) ? "в стенах" : "в полах";
                bool flag = true;
                while (flag)
                {
                    MergeOpeningsWindow mergeOpeningsWindow = new MergeOpeningsWindow(document, list, titleSuffix, kind);
                    new WindowInteropHelper(mergeOpeningsWindow).Owner = application.MainWindowHandle;
                    if (!mergeOpeningsWindow.ShowDialog().GetValueOrDefault() || mergeOpeningsWindow.ResultAction == MergeAction.Cancel)
                    {
                        return Result.Cancelled;
                    }

                    FamilySymbol selectedSymbol = mergeOpeningsWindow.SelectedSymbol;
                    string widthParamName = mergeOpeningsWindow.WidthParam;
                    string heightParam = mergeOpeningsWindow.HeightParam;
                    string depthParamName = mergeOpeningsWindow.DepthParam;

                    double offset = UnitUtils.ConvertToInternalUnits(mergeOpeningsWindow.OffsetMm, UnitTypeId.Millimeters);
                    double depthOffset = UnitUtils.ConvertToInternalUnits(mergeOpeningsWindow.DepthOffsetMm, UnitTypeId.Millimeters);

                    if (mergeOpeningsWindow.ResultAction == MergeAction.ManualPick)
                    {
                        IList<Reference> list2 = null;
                        try
                        {
                            list2 = activeUIDocument.Selection.PickObjects(
                                ObjectType.Element, 
                                new OpeningSelectionFilter(kind), 
                                "Выберите отверстия для объединения и нажмите 'Готово' в левом верхнем углу"
                            );
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            continue;
                        }

                        if (list2 == null || list2.Count < 2)
                        {
                            TaskDialog.Show("Внимание", "Для объединения необходимо выбрать как минимум 2 отверстия.");
                        }
                        else
                        {
                            List<FamilyInstance> list3 = new List<FamilyInstance>();
                            foreach (Reference current in list2)
                            {
                                FamilyInstance familyInstance = document.GetElement(current) as FamilyInstance;
                                if (familyInstance != null)
                                {
                                    list3.Add(familyInstance);
                                }
                            }

                            FamilySymbol symbol = list3[0].Symbol;
                            string detectedWidthParam;
                            string detectedHeightParam;
                            string detectedDepthParam;
                            OpeningsRunner.AutoDetectParamNames(document, symbol, widthParamName, heightParam, depthParamName, out detectedWidthParam, out detectedHeightParam, out detectedDepthParam);

                            using (Transaction transaction = new Transaction(document, "Объединение выбранных отверстий"))
							{
								transaction.Start();
								if (!symbol.IsActive)
								{
									symbol.Activate();
									document.Regenerate();
								}
								OpeningsRunner.MergeSelectedFamilyInstances(document, list3, symbol, detectedWidthParam, detectedHeightParam, detectedDepthParam, offset, depthOffset, kind);
								transaction.Commit();
							}
							TaskDialog.Show("Готово", string.Format("Объединено выбранных элементов: {0}", list3.Count));
							flag = false;
						}
					}
					else if (mergeOpeningsWindow.ResultAction == MergeAction.BatchMerge)
					{
						FamilySymbol sourceSymbol = mergeOpeningsWindow.SourceSymbol;
						double mergeDistanceMm = mergeOpeningsWindow.MergeDistanceMm;
						bool useCurrentViewOnly = mergeOpeningsWindow.UseCurrentViewOnly;
						double mergeThresholdFeet = mergeDistanceMm / 304.8;

						var allInstances = (useCurrentViewOnly 
							? new FilteredElementCollector(document, document.ActiveView.Id) 
							: new FilteredElementCollector(document))
							.OfClass(typeof(FamilyInstance))
							.Cast<FamilyInstance>()
							.Where(fi => fi.Symbol.Id == sourceSymbol.Id)
							.ToList();

						List<FamilyInstance> list4 = new List<FamilyInstance>();
						foreach (FamilyInstance current2 in allInstances)
						{
							XYZ handOrientation = current2.HandOrientation;
							XYZ facingOrientation = current2.FacingOrientation;
							XYZ xYZ = (handOrientation != null && facingOrientation != null) ? handOrientation.CrossProduct(facingOrientation).Normalize() : null;
							
							bool flag2 = kind == OpeningsRunner.HostKind.Wall;
							if (current2.Host != null && (current2.Host is Wall || current2.Host is Floor || current2.Host is Ceiling || 
								(current2.Host.Category != null && (current2.Host.Category.Id.IntegerValue == -2000032 || current2.Host.Category.Id.IntegerValue == -2000038))))
							{
								flag2 = (current2.Host is Wall);
							}
							else
							{
								string famName = current2.Symbol?.Family?.Name?.ToLowerInvariant() ?? "";
								string symName = current2.Symbol?.Name?.ToLowerInvariant() ?? "";
								
								bool flag3 = famName.Contains("стен") || famName.Contains("wall") || symName.Contains("стен") || symName.Contains("wall");
								bool flag4 = famName.Contains("пол") || famName.Contains("перекрыт") || famName.Contains("floor") || famName.Contains("slab") || famName.Contains("ceiling") || 
											 symName.Contains("пол") || symName.Contains("перекрыт") || symName.Contains("floor") || symName.Contains("slab") || symName.Contains("ceiling");
								if (flag3 && !flag4)
								{
									flag2 = true;
								}
								else if (flag4 && !flag3)
								{
									flag2 = false;
								}
								else
								{
									if (xYZ != null)
									{
										flag2 = (Math.Abs(xYZ.Z) < 0.5);
									}
									else if (facingOrientation != null)
									{
										flag2 = (Math.Abs(facingOrientation.Z) < 0.5);
									}
								}
							}
							
							if (kind == OpeningsRunner.HostKind.Wall && flag2)
							{
								list4.Add(current2);
							}
							else if (kind == OpeningsRunner.HostKind.Floor && !flag2)
							{
								list4.Add(current2);
							}
						}

						if (list4.Count < 2)
						{
							TaskDialog.Show("Информация", "Не найдено достаточного количества отверстий выбранного типа для объединения.");
						}
						else
						{
							int num = 0;
							int num2 = 0;
							
							Func<FamilyInstance, FamilyInstance, bool> areConnected = (fi1, fi2) =>
							{
								LocationPoint lp1 = fi1.Location as LocationPoint;
								XYZ pt1 = (lp1 != null ? lp1.Point : XYZ.Zero);
								LocationPoint lp2 = fi2.Location as LocationPoint;
								XYZ pt2 = (lp2 != null ? lp2.Point : XYZ.Zero);
								
								if (kind == OpeningsRunner.HostKind.Wall)
								{
									XYZ facing1 = fi1.FacingOrientation ?? XYZ.BasisY;
									XYZ facing2 = fi2.FacingOrientation ?? XYZ.BasisY;
									if (Math.Abs(facing1.DotProduct(facing2)) < 0.9)
									{
										return false;
									}
									if (Math.Abs((pt2 - pt1).DotProduct(facing1)) > 3.0)
									{
										return false;
									}
									XYZ hand1 = fi1.HandOrientation ?? XYZ.BasisX;
									XYZ up1 = XYZ.BasisZ;
									
									Func<FamilyInstance, OpeningsRunner.LocalRect> getLocalRect = fi =>
									{
										double w = 0.0;
										double d = 0.0;
										Parameter wp = string.IsNullOrEmpty(widthParamName) ? null : fi.LookupParameter(widthParamName);
										if (wp != null && wp.HasValue) w = wp.AsDouble();
										Parameter dp = string.IsNullOrEmpty(depthParamName) ? null : fi.LookupParameter(depthParamName);
										if (dp != null && dp.HasValue) d = dp.AsDouble();
										
										if (w <= 0.0 || d <= 0.0)
										{
											BoundingBoxXYZ bbox = fi.get_BoundingBox(null);
											if (bbox != null)
											{
												w = Math.Abs(bbox.Max.X - bbox.Min.X);
												d = Math.Abs(bbox.Max.Z - bbox.Min.Z);
											}
										}
										LocationPoint lp = fi.Location as LocationPoint;
										XYZ offsetVec = (lp != null ? lp.Point : XYZ.Zero) - pt1;
										double u = offsetVec.DotProduct(hand1);
										double v = offsetVec.DotProduct(up1);
										return new OpeningsRunner.LocalRect
										{
											UMin = u - w / 2.0,
											UMax = u + w / 2.0,
											VMin = v - d / 2.0,
											VMax = v + d / 2.0
										};
									};
									
									return getLocalRect(fi1).DistanceTo(getLocalRect(fi2)) < mergeThresholdFeet;
								}
								else
								{
									if (Math.Abs(pt1.Z - pt2.Z) > 1.5)
									{
										return false;
									}
									XYZ hand1 = fi1.HandOrientation ?? XYZ.BasisX;
									if (Math.Abs(hand1.Z) > 0.9)
									{
										hand1 = XYZ.BasisX;
									}
									XYZ dirU1 = new XYZ(hand1.X, hand1.Y, 0.0);
									if (dirU1.GetLength() < 1E-09)
									{
										dirU1 = XYZ.BasisX;
									}
									else
									{
										dirU1 = dirU1.Normalize();
									}
									XYZ dirV1 = XYZ.BasisZ.CrossProduct(dirU1).Normalize();
									
									Func<FamilyInstance, OpeningsRunner.LocalRect> getLocalRectFloor = fi =>
									{
										double w = 0.0;
										double d = 0.0;
										Parameter wp = string.IsNullOrEmpty(widthParamName) ? null : fi.LookupParameter(widthParamName);
										if (wp != null && wp.HasValue) w = wp.AsDouble();
										Parameter dp = string.IsNullOrEmpty(depthParamName) ? null : fi.LookupParameter(depthParamName);
										if (dp != null && dp.HasValue) d = dp.AsDouble();
										
										if (w <= 0.0 || d <= 0.0)
										{
											BoundingBoxXYZ bbox = fi.get_BoundingBox(null);
											if (bbox != null)
											{
												w = Math.Abs(bbox.Max.X - bbox.Min.X);
												d = Math.Abs(bbox.Max.Y - bbox.Min.Y);
											}
										}
										LocationPoint lp = fi.Location as LocationPoint;
										XYZ offsetVec = (lp != null ? lp.Point : XYZ.Zero) - pt1;
										double u = offsetVec.DotProduct(dirU1);
										double v = offsetVec.DotProduct(dirV1);
										return new OpeningsRunner.LocalRect
										{
											UMin = u - w / 2.0,
											UMax = u + w / 2.0,
											VMin = v - d / 2.0,
											VMax = v + d / 2.0
										};
									};
									
									return getLocalRectFloor(fi1).DistanceTo(getLocalRectFloor(fi2)) < mergeThresholdFeet;
								}
							};

							List<List<FamilyInstance>> connectedComponents = OpeningsRunner.GetConnectedComponents<FamilyInstance>(list4, areConnected);
							using (Transaction transaction2 = new Transaction(document, "Пакетное объединение отверстий"))
							{
								transaction2.Start();
								if (!selectedSymbol.IsActive)
								{
									selectedSymbol.Activate();
									document.Regenerate();
								}
								foreach (List<FamilyInstance> current3 in connectedComponents)
								{
									if (current3.Count >= 2)
									{
										OpeningsRunner.MergeSelectedFamilyInstances(document, current3, selectedSymbol, widthParamName, heightParam, depthParamName, offset, depthOffset, kind);
										num++;
										num2 += current3.Count;
									}
								}
								transaction2.Commit();
							}
							TaskDialog.Show("Готово", string.Format("Пакетное объединение завершено.\nСоздано объединенных отверстий: {0}\nУдалено исходных отверстий: {1}", num, num2));
							flag = false;
						}
					}
				}
				return Result.Succeeded;
			}
			catch (Exception ex)
			{
				message = ex.Message;
				Logger.LogError("Ошибка при объединении отверстий", ex);
				return Result.Failed;
			}
		}

        private static void MergeSelectedFamilyInstances(Document doc, List<FamilyInstance> selectedFamilies, FamilySymbol openingSymbol, string widthParamName, string heightParamName, string depthParamName, double offset, double depthOffset, OpeningsRunner.HostKind kind)
        {
            FamilyInstance familyInstance = selectedFamilies[0];
            LocationPoint lp1 = familyInstance.Location as LocationPoint;
            XYZ xYZ = (lp1 != null ? lp1.Point : XYZ.Zero);
            XYZ xYZ2 = familyInstance.FacingOrientation ?? XYZ.BasisY;
            XYZ xYZ3 = familyInstance.HandOrientation ?? XYZ.BasisX;
            XYZ xYZ4 = XYZ.BasisZ;
            
            if (kind == OpeningsRunner.HostKind.Floor)
            {
                xYZ2 = XYZ.BasisZ;
                xYZ3 = (familyInstance.HandOrientation ?? XYZ.BasisX);
                if (Math.Abs(xYZ3.Z) > 0.9)
                {
                    xYZ3 = XYZ.BasisX;
                }
                xYZ4 = xYZ2.CrossProduct(xYZ3).Normalize();
            }
            else
            {
                xYZ4 = XYZ.BasisZ;
                xYZ2 = new XYZ(xYZ2.X, xYZ2.Y, 0.0);
                if (xYZ2.GetLength() < 1E-09)
                {
                    xYZ2 = XYZ.BasisY;
                }
                else
                {
                    xYZ2 = xYZ2.Normalize();
                }
                xYZ3 = xYZ4.CrossProduct(xYZ2).Normalize();
            }

            List<XYZ> list = new List<XYZ>();
            foreach (FamilyInstance current in selectedFamilies)
            {
                List<XYZ> geometryVertices = OpeningsRunner.GetGeometryVertices(current);
                if (geometryVertices.Count == 0)
                {
                    BoundingBoxXYZ bbox = current.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        geometryVertices.Add(bbox.Min);
                        geometryVertices.Add(bbox.Max);
                        geometryVertices.Add(new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z));
                        geometryVertices.Add(new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z));
                        geometryVertices.Add(new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z));
                        geometryVertices.Add(new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z));
                    }
                }
                list.AddRange(geometryVertices);
            }

            if (list.Count == 0)
            {
                throw new Exception("Не удалось получить геометрию выбранных элементов.");
            }

            double num = double.MaxValue;
            double num2 = double.MinValue;
            double num3 = double.MaxValue;
            double num4 = double.MinValue;
            double num5 = double.MaxValue;
            double num6 = double.MinValue;

            foreach (XYZ pt in list)
            {
                XYZ offsetVec = pt - xYZ;
                double num7 = offsetVec.DotProduct(xYZ3);
                double num8 = offsetVec.DotProduct(xYZ4);
                double num9 = offsetVec.DotProduct(xYZ2);
                
                if (num7 < num) num = num7;
                if (num7 > num2) num2 = num7;
                if (num8 < num3) num3 = num8;
                if (num8 > num4) num4 = num8;
                if (num9 < num5) num5 = num9;
                if (num9 > num6) num6 = num9;
            }

            double value = num2 - num + offset;
            double value2 = num4 - num3 + offset;
            double value3 = num6 - num5 + depthOffset;
            double value4 = (num + num2) / 2.0;
            double value5 = (num3 + num4) / 2.0;
            double value6 = (num5 + num6) / 2.0;
            
            XYZ xYZ5 = xYZ + value4 * xYZ3 + value5 * xYZ4 + value6 * xYZ2;
            Element hostElem = null;
            foreach (FamilyInstance current2 in selectedFamilies)
            {
                if (current2.Host != null)
                {
                    hostElem = current2.Host;
                    break;
                }
            }

            FamilyInstance familyInstance2 = null;
            if (hostElem != null)
            {
                familyInstance2 = doc.Create.NewFamilyInstance(xYZ5, openingSymbol, hostElem, StructuralType.NonStructural);
            }
            else
            {
                familyInstance2 = doc.Create.NewFamilyInstance(xYZ5, openingSymbol, StructuralType.NonStructural);
            }

            if (familyInstance2 == null)
            {
                throw new Exception("Не удалось создать новое семейство отверстия.");
            }

            doc.Regenerate();
            if (kind == OpeningsRunner.HostKind.Wall)
            {
                try
                {
                    XYZ xYZ6 = new XYZ(xYZ2.X, xYZ2.Y, 0.0);
                    xYZ6 = ((xYZ6.GetLength() > 1E-09) ? xYZ6.Normalize() : XYZ.BasisY);
                    XYZ xYZ7 = familyInstance2.FacingOrientation ?? XYZ.BasisY;
                    XYZ xYZ8 = new XYZ(xYZ7.X, xYZ7.Y, 0.0);
                    xYZ8 = ((xYZ8.GetLength() > 1E-09) ? xYZ8.Normalize() : XYZ.BasisY);
                    double num10 = Math.Atan2(xYZ8.Y, xYZ8.X);
                    double num11 = Math.Atan2(xYZ6.Y, xYZ6.X) - num10;
                    if (Math.Abs(num11) > 1E-09)
                    {
                        Autodesk.Revit.DB.Line axis = Autodesk.Revit.DB.Line.CreateBound(xYZ5, xYZ5 + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, familyInstance2.Id, axis, num11);
                    }
                }
                catch { }
            }
            
            if (kind == OpeningsRunner.HostKind.Floor)
            {
                try
                {
                    XYZ xYZ9 = familyInstance2.HandOrientation ?? XYZ.BasisX;
                    XYZ xYZ10 = new XYZ(xYZ9.X, xYZ9.Y, 0.0);
                    xYZ10 = ((xYZ10.GetLength() > 1E-09) ? xYZ10.Normalize() : XYZ.BasisX);
                    XYZ xYZ11 = new XYZ(xYZ3.X, xYZ3.Y, 0.0);
                    xYZ11 = ((xYZ11.GetLength() > 1E-09) ? xYZ11.Normalize() : XYZ.BasisX);
                    double num12 = Math.Atan2(xYZ10.Y, xYZ10.X);
                    double num13 = Math.Atan2(xYZ11.Y, xYZ11.X) - num12;
                    if (Math.Abs(num13) > 1E-09)
                    {
                        Autodesk.Revit.DB.Line axis2 = Autodesk.Revit.DB.Line.CreateBound(xYZ5, xYZ5 + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, familyInstance2.Id, axis2, num13);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка при повороте отверстия в полу", ex);
                }
                
                OpeningsRunner.SetParam(familyInstance2, widthParamName, value2);
                OpeningsRunner.SetParam(familyInstance2, depthParamName, value);
                OpeningsRunner.SetParam(familyInstance2, heightParamName, value3);
            }
            else
            {
                OpeningsRunner.SetParam(familyInstance2, widthParamName, value);
                OpeningsRunner.SetParam(familyInstance2, heightParamName, value2);
                OpeningsRunner.SetParam(familyInstance2, depthParamName, value3);
            }

            foreach (FamilyInstance current3 in selectedFamilies)
            {
                doc.Delete(current3.Id);
            }
        }

        private static List<List<T>> GetConnectedComponents<T>(List<T> items, Func<T, T, bool> areConnected)
        {
            List<List<T>> list = new List<List<T>>();
            HashSet<int> hashSet = new HashSet<int>();
            for (int i = 0; i < items.Count; i++)
            {
                if (!hashSet.Contains(i))
                {
                    List<T> list2 = new List<T>();
                    Queue<int> queue = new Queue<int>();
                    queue.Enqueue(i);
                    hashSet.Add(i);
                    while (queue.Count > 0)
                    {
                        int index = queue.Dequeue();
                        list2.Add(items[index]);
                        for (int j = 0; j < items.Count; j++)
                        {
                            if (!hashSet.Contains(j) && areConnected(items[index], items[j]))
                            {
                                hashSet.Add(j);
                                queue.Enqueue(j);
                            }
                        }
                    }
                    list.Add(list2);
                }
            }
            return list;
        }

        private class LocalRect
        {
            public double UMin;
            public double UMax;
            public double VMin;
            public double VMax;
            public double DistanceTo(OpeningsRunner.LocalRect other)
            {
                double num = 0.0;
                if (this.UMax < other.UMin)
                {
                    num = other.UMin - this.UMax;
                }
                else if (other.UMax < this.UMin)
                {
                    num = this.UMin - other.UMax;
                }
                double num2 = 0.0;
                if (this.VMax < other.VMin)
                {
                    num2 = other.VMin - this.VMax;
                }
                else if (other.VMax < this.VMin)
                {
                    num2 = this.VMin - other.VMax;
                }
                return Math.Sqrt(num * num + num2 * num2);
            }
        }

        public static void AutoDetectParamNames(Document doc, FamilySymbol symbol, string uiWidthParam, string uiHeightParam, string uiDepthParam, out string widthParamName, out string heightParamName, out string depthParamName)
        {
            widthParamName = null;
            heightParamName = null;
            depthParamName = null;
            List<string> editableDoubleParamNames = OpeningsRunner.GetEditableDoubleParamNames(doc, symbol);
            
            widthParamName = editableDoubleParamNames.FirstOrDefault(p => p.IndexOf("ширин", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("width", StringComparison.OrdinalIgnoreCase) >= 0);
            heightParamName = editableDoubleParamNames.FirstOrDefault(p => p.IndexOf("высот", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("height", StringComparison.OrdinalIgnoreCase) >= 0);
            depthParamName = editableDoubleParamNames.FirstOrDefault(p => p.IndexOf("глубин", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("толщин", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("depth", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("thick", StringComparison.OrdinalIgnoreCase) >= 0);
            
            if (string.IsNullOrEmpty(depthParamName))
            {
                depthParamName = editableDoubleParamNames.FirstOrDefault(p => p.IndexOf("длин", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("length", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (string.IsNullOrEmpty(widthParamName))
            {
                if (!string.IsNullOrEmpty(uiWidthParam) && editableDoubleParamNames.Contains(uiWidthParam))
                {
                    widthParamName = uiWidthParam;
                }
                else
                {
                    widthParamName = editableDoubleParamNames.FirstOrDefault();
                }
            }
            string localWidth = widthParamName;
            if (string.IsNullOrEmpty(heightParamName))
            {
                if (!string.IsNullOrEmpty(uiHeightParam) && editableDoubleParamNames.Contains(uiHeightParam))
                {
                    heightParamName = uiHeightParam;
                }
                else
                {
                    heightParamName = editableDoubleParamNames.FirstOrDefault(p => p != localWidth);
                }
            }
            string localHeight = heightParamName;
            if (string.IsNullOrEmpty(depthParamName))
            {
                if (!string.IsNullOrEmpty(uiDepthParam) && editableDoubleParamNames.Contains(uiDepthParam))
                {
                    depthParamName = uiDepthParam;
                }
                else
                {
                    depthParamName = editableDoubleParamNames.FirstOrDefault(p => p != localWidth && p != localHeight);
                }
            }
            if (string.IsNullOrEmpty(widthParamName))
            {
                widthParamName = "Ширина";
            }
            if (string.IsNullOrEmpty(heightParamName))
            {
                heightParamName = "Высота";
            }
            if (string.IsNullOrEmpty(depthParamName))
            {
                depthParamName = "Толщина";
            }
        }

        private static List<XYZ> GetGeometryVertices(Element element)
        {
            List<XYZ> list = new List<XYZ>();
            try
            {
                Options options = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    ComputeReferences = false
                };
                GeometryElement geometryElement = element.get_Geometry(options);
                if (geometryElement != null)
                {
                    OpeningsRunner.ParseGeometry(geometryElement, list);
                }
            }
            catch
            {
            }
            return list;
        }

        private static void ParseGeometry(GeometryObject geomObj, List<XYZ> vertices)
        {
            GeometryElement geometryElement = geomObj as GeometryElement;
            if (geometryElement != null)
            {
                foreach (GeometryObject current in geometryElement)
                {
                    OpeningsRunner.ParseGeometry(current, vertices);
                }
                return;
            }
            GeometryInstance geometryInstance = geomObj as GeometryInstance;
            if (geometryInstance != null)
            {
                GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                if (instanceGeometry != null)
                {
                    foreach (GeometryObject current in instanceGeometry)
                    {
                        OpeningsRunner.ParseGeometry(current, vertices);
                    }
                    return;
                }
                GeometryElement symbolGeometry = geometryInstance.GetSymbolGeometry();
                if (symbolGeometry != null)
                {
                    Autodesk.Revit.DB.Transform transform = geometryInstance.Transform;
                    List<XYZ> list = new List<XYZ>();
                    foreach (GeometryObject current in symbolGeometry)
                    {
                        OpeningsRunner.ParseGeometry(current, list);
                    }
                    foreach (XYZ current2 in list)
                    {
                        vertices.Add(transform.OfPoint(current2));
                    }
                }
                return;
            }
            Solid solid = geomObj as Solid;
            if (solid != null && solid.Volume > 1E-09)
            {
                foreach (Edge edge in solid.Edges)
                {
                    vertices.Add(edge.Evaluate(0.0));
                    vertices.Add(edge.Evaluate(1.0));
                }
            }
        }
    }

    // ========================================================================
    // Окно выбора
    // ========================================================================
    public class FamilySelectionWindow : Window
    {
        private Document _doc;
        private ComboBox _comboFamily;
        private ComboBox _comboWidth;
        private ComboBox _comboHeight;
        private ComboBox _comboDepth;
        private TextBox _offsetBox;
        private TextBox _depthOffsetBox;
        private TextBox _searchBox;
        private CheckBox _chkSelectedOnly;
        private CheckBox _chkCurrentView;
        private CheckBox _chkDucts;
        private CheckBox _chkPipes;
        private CheckBox _chkTrays;

        public FamilySymbol SelectedSymbol { get; private set; }
        public double OffsetMm { get; private set; } = 50;
        public double DepthOffsetMm { get; private set; } = 50;
        public string WidthParam { get; private set; }
        public string HeightParam { get; private set; }
        public string DepthParam { get; private set; }
        public bool UseSelectedOnly { get; private set; } = false;
        public bool UseCurrentViewOnly { get; private set; } = false;
        public bool UseDucts { get; private set; } = true;
        public bool UsePipes { get; private set; } = true;
        public bool UseTrays { get; private set; } = true;

        private List<SymbolItem> _allItems;

        private class SymbolItem
        {
            public string Display { get; set; }
            public FamilySymbol Symbol { get; set; }
            public override string ToString() => Display;
        }

        private Style GetRoundedButtonStyle(double radius)
        {
            Style style = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

        public FamilySelectionWindow(Document doc, List<FamilySymbol> symbols, bool hasPreselection, string titleSuffix)
        {
            _doc = doc;
            Title = "Параметры расстановки отверстий " + titleSuffix + " - BimboClub Tools";
            Width = 620;
            Height = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            // Настройка темы оформления BimboClub (Темная тема)
            this.Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            this.FontFamily = new FontFamily("Segoe UI");
            this.FontSize = 13;
            this.Foreground = Brushes.White;

            // Установка иконки
            try
            {
                string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string logoPath = Path.Combine(assemblyDir, "icon32.png");
                if (File.Exists(logoPath))
                {
                    this.Icon = new BitmapImage(new Uri(logoPath));
                }
            }
            catch { }

            // Кисти
            SolidColorBrush bgBrush = new SolidColorBrush(Color.FromRgb(45, 45, 53)); // #2D2D35
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038

            Style comboBoxItemStyle = new Style(typeof(ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, bgBrush));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(ComboBoxItem), comboBoxItemStyle);

            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, bgBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(3)));

            // 1. Заголовок с ало-черным градиентом
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 18, 20),
                    Color.FromRgb(179, 14, 45), // #B30E2D
                    0.0),
                Padding = new Thickness(15, 10, 15, 10),
                Height = 50
            };
            TextBlock headerText = new TextBlock
            {
                Text = "РАССТАНОВКА ОТВЕРСТИЙ " + titleSuffix.ToUpper(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            this.Content = mainGrid;

            mainGrid.Children.Add(headerBorder);
            Grid.SetRow(headerBorder, 0);

            // 2. Сетка полей ввода
            Grid grid = new Grid { Margin = new Thickness(15) };
            Grid.SetRow(grid, 1);
            mainGrid.Children.Add(grid);

            for (int i = 0; i < 13; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // Типы элементов
            AddLabel(grid, "Типы элементов:", row);
            StackPanel typesPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _chkDucts = new CheckBox { Content = "Воздуховоды", IsChecked = true, Margin = new Thickness(0, 0, 12, 0), Foreground = Brushes.White };
            _chkPipes = new CheckBox { Content = "Трубы", IsChecked = true, Margin = new Thickness(0, 0, 12, 0), Foreground = Brushes.White };
            _chkTrays = new CheckBox { Content = "Лотки", IsChecked = true, Foreground = Brushes.White };
            typesPanel.Children.Add(_chkDucts);
            typesPanel.Children.Add(_chkPipes);
            typesPanel.Children.Add(_chkTrays);
            Grid.SetRow(typesPanel, row); Grid.SetColumn(typesPanel, 1);
            grid.Children.Add(typesPanel);
            row++;

            AddLabel(grid, "Поиск:", row);
            _searchBox = new TextBox { Style = textBoxStyle, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(_searchBox, row); Grid.SetColumn(_searchBox, 1);
            grid.Children.Add(_searchBox);
            row++;

            AddLabel(grid, "Семейство (тип):", row);
            _comboFamily = new ComboBox { Margin = new Thickness(0, 0, 0, 10), Background = bgBrush, Foreground = Brushes.White, BorderBrush = borderBrush };
            _allItems = symbols.Select(s => new SymbolItem
            {
                Symbol = s,
                Display = $"{s.Family.Name} : {s.Name}"
            }).ToList();
            _comboFamily.ItemsSource = _allItems;

            var defaultItem = _allItems.FirstOrDefault(i =>
                i.Display.ToLower().Contains("отверстие") ||
                i.Display.ToLower().Contains("opening"));
            if (defaultItem != null) _comboFamily.SelectedItem = defaultItem;
            else if (_allItems.Count > 0) _comboFamily.SelectedIndex = 0;

            Grid.SetRow(_comboFamily, row); Grid.SetColumn(_comboFamily, 1);
            grid.Children.Add(_comboFamily);
            row++;

            _searchBox.TextChanged += (s, e) =>
            {
                string text = _searchBox.Text.ToLower();
                var filtered = string.IsNullOrWhiteSpace(text)
                    ? _allItems
                    : _allItems.Where(i => i.Display.ToLower().Contains(text)).ToList();
                _comboFamily.ItemsSource = filtered;
                if (_comboFamily.Items.Count > 0) _comboFamily.SelectedIndex = 0;
            };

            AddLabel(grid, "Параметр ширины:", row);
            _comboWidth = new ComboBox { Margin = new Thickness(0, 0, 0, 5), Background = bgBrush, Foreground = Brushes.White, BorderBrush = borderBrush };
            Grid.SetRow(_comboWidth, row); Grid.SetColumn(_comboWidth, 1);
            grid.Children.Add(_comboWidth);
            row++;

            AddLabel(grid, "Параметр высоты:", row);
            _comboHeight = new ComboBox { Margin = new Thickness(0, 0, 0, 5), Background = bgBrush, Foreground = Brushes.White, BorderBrush = borderBrush };
            Grid.SetRow(_comboHeight, row); Grid.SetColumn(_comboHeight, 1);
            grid.Children.Add(_comboHeight);
            row++;

            AddLabel(grid, "Параметр глубины (толщины):", row);
            _comboDepth = new ComboBox { Margin = new Thickness(0, 0, 0, 10), Background = bgBrush, Foreground = Brushes.White, BorderBrush = borderBrush };
            Grid.SetRow(_comboDepth, row); Grid.SetColumn(_comboDepth, 1);
            grid.Children.Add(_comboDepth);
            row++;

            AddLabel(grid, "Запас по ширине/высоте\n(на сторону), мм:", row);
            _offsetBox = new TextBox { Style = textBoxStyle, Text = "50", Margin = new Thickness(0, 0, 0, 5) };
            _offsetBox.ToolTip = "Прибавится к ширине и высоте отверстия с двух сторон (суммарно прибавится 2 * запас)";
            Grid.SetRow(_offsetBox, row); Grid.SetColumn(_offsetBox, 1);
            grid.Children.Add(_offsetBox);
            row++;

            AddLabel(grid, "Запас по глубине, мм:", row);
            _depthOffsetBox = new TextBox { Style = textBoxStyle, Text = "50", Margin = new Thickness(0, 0, 0, 10) };
            _depthOffsetBox.ToolTip = "Прибавится к толщине стены/пола";
            Grid.SetRow(_depthOffsetBox, row); Grid.SetColumn(_depthOffsetBox, 1);
            grid.Children.Add(_depthOffsetBox);
            row++;

            _chkSelectedOnly = new CheckBox
            {
                Content = hasPreselection
                    ? "Использовать только выбранные элементы"
                    : "Использовать только выбранные элементы (нет выделения)",
                IsEnabled = hasPreselection,
                IsChecked = hasPreselection,
                Margin = new Thickness(0, 3, 0, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            Grid.SetRow(_chkSelectedOnly, row); Grid.SetColumn(_chkSelectedOnly, 1);
            grid.Children.Add(_chkSelectedOnly);
            row++;

            _chkCurrentView = new CheckBox
            {
                Content = "Только элементы на текущем виде",
                IsChecked = false,
                Margin = new Thickness(0, 3, 0, 10),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            Grid.SetRow(_chkCurrentView, row); Grid.SetColumn(_chkCurrentView, 1);
            grid.Children.Add(_chkCurrentView);
            row++;

            _chkSelectedOnly.Checked += (s, e) => { if (_chkCurrentView != null) _chkCurrentView.IsChecked = false; };
            _chkCurrentView.Checked += (s, e) => { if (_chkSelectedOnly != null) _chkSelectedOnly.IsChecked = false; };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(buttons, row);
            Grid.SetColumn(buttons, 1);

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            Button okBtn = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true,
                Style = roundedButtonStyle,
                Background = new SolidColorBrush(Color.FromRgb(179, 14, 45)), // Brand crimson
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            okBtn.Click += OkBtn_Click;
            buttons.Children.Add(okBtn);

            Button cancelBtn = new Button
            {
                Content = "Отмена",
                Width = 100,
                Height = 32,
                IsCancel = true,
                Style = roundedButtonStyle,
                Background = new SolidColorBrush(Color.FromRgb(58, 58, 66)), // Dark slate
                Foreground = Brushes.White
            };
            buttons.Children.Add(cancelBtn);

            grid.Children.Add(buttons);

            // Применяем красивый стиль ко всем комбобоксам
            UiThemeHelper.StyleComboBoxDark(_comboFamily, bgBrush, borderBrush);
            UiThemeHelper.StyleComboBoxDark(_comboWidth, bgBrush, borderBrush);
            UiThemeHelper.StyleComboBoxDark(_comboHeight, bgBrush, borderBrush);
            UiThemeHelper.StyleComboBoxDark(_comboDepth, bgBrush, borderBrush);

            _comboFamily.SelectionChanged += (s, e) => UpdateParamLists();
            UpdateParamLists();
        }

        private void AddLabel(Grid grid, string text, int row)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 5)
            };
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, 0);
            grid.Children.Add(tb);
        }

        private void UpdateParamLists()
        {
            if (_comboFamily == null || _comboWidth == null || _comboHeight == null || _comboDepth == null)
                return;

            var item = _comboFamily.SelectedItem as SymbolItem;
            if (item == null || item.Symbol == null) return;

            List<string> names;
            try { names = DuctWallOpeningsCommand.GetEditableDoubleParamNames(_doc, item.Symbol); }
            catch { names = new List<string>(); }

            var withEmpty = new List<string> { "— не задавать —" };
            withEmpty.AddRange(names);

            _comboWidth.ItemsSource = withEmpty.ToList();
            _comboHeight.ItemsSource = withEmpty.ToList();
            _comboDepth.ItemsSource = withEmpty.ToList();

            _comboWidth.SelectedItem = FindByKeywords(names, "ширин", "width") ?? withEmpty[0];
            _comboHeight.SelectedItem = FindByKeywords(names, "высот", "height") ?? withEmpty[0];
            _comboDepth.SelectedItem = FindByKeywords(names, "глубин", "толщин", "depth", "thick") ?? withEmpty[0];
        }

        private string FindByKeywords(List<string> names, params string[] keys)
        {
            foreach (var key in keys)
            {
                var found = names.FirstOrDefault(n => n.ToLower().Contains(key));
                if (found != null) return found;
            }
            return null;
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            var item = _comboFamily.SelectedItem as SymbolItem;
            if (item == null)
            {
                MessageBox.Show("Выберите семейство.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedSymbol = item.Symbol;

            WidthParam = NormalizeParam(_comboWidth.SelectedItem as string);
            HeightParam = NormalizeParam(_comboHeight.SelectedItem as string);
            DepthParam = NormalizeParam(_comboDepth.SelectedItem as string);

            if (double.TryParse(_offsetBox.Text.Replace('.', ','), out double off) && off >= 0)
                OffsetMm = off;
            else OffsetMm = 50;

            if (double.TryParse(_depthOffsetBox.Text.Replace('.', ','), out double doff) && doff >= 0)
                DepthOffsetMm = doff;
            else DepthOffsetMm = 50;

            UseSelectedOnly = _chkSelectedOnly?.IsChecked == true;
            UseCurrentViewOnly = _chkCurrentView?.IsChecked == true;
            UseDucts = _chkDucts?.IsChecked == true;
            UsePipes = _chkPipes?.IsChecked == true;
            UseTrays = _chkTrays?.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private string NormalizeParam(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "— не задавать —") return null;
            return s;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MergeWallOpeningsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OpeningsRunner.RunMerge(commandData, ref message, OpeningsRunner.HostKind.Wall);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MergeFloorOpeningsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OpeningsRunner.RunMerge(commandData, ref message, OpeningsRunner.HostKind.Floor);
        }
    }

    public class OpeningSelectionFilter : ISelectionFilter
    {
        private OpeningsRunner.HostKind _kind;
        public OpeningSelectionFilter(OpeningsRunner.HostKind kind)
        {
            this._kind = kind;
        }
        public bool AllowElement(Element elem)
        {
            return elem is FamilyInstance;
        }
        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    public enum MergeAction
    {
        Cancel,
        ManualPick,
        BatchMerge
    }

    public class MergeOpeningsWindow : Window
    {
        public class SymbolItem
        {
            public string Display { get; set; }
            public FamilySymbol Symbol { get; set; }
            public override string ToString() => Display;
        }

        private Document _doc;
        private TextBox _searchBox;
        private ComboBox _comboSourceFamily;
        private CheckBox _chkCurrentView;
        private CheckBox _chkAllProject;
        private TextBox _txtMergeDistance;
        private List<SymbolItem> _allItems;

        public FamilySymbol SelectedSymbol { get; private set; }
        public double OffsetMm { get; private set; }
        public double DepthOffsetMm { get; private set; }
        public string WidthParam { get; private set; }
        public string HeightParam { get; private set; }
        public string DepthParam { get; private set; }
        public MergeAction ResultAction { get; private set; }
        public FamilySymbol SourceSymbol { get; private set; }
        public bool UseCurrentViewOnly { get; private set; }
        public double MergeDistanceMm { get; private set; }

        public MergeOpeningsWindow(Document doc, List<FamilySymbol> symbols, string titleSuffix, OpeningsRunner.HostKind kind)
        {
            UseCurrentViewOnly = true;
            MergeDistanceMm = 200.0;
            _doc = doc;
            Title = "Объединение отверстий " + titleSuffix + " - BimboClub Tools";
            Width = 620.0;
            Height = 480.0;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13.0;

            SolidColorBrush panelBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 48));
            SolidColorBrush borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 56));
            SolidColorBrush accentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));

            try
            {
                string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string logoPath = Path.Combine(assemblyDir, "icon32.png");
                if (File.Exists(logoPath))
                {
                    this.Icon = new BitmapImage(new Uri(logoPath));
                }
            }
            catch { }

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            this.Content = grid;

            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(18, 18, 20), System.Windows.Media.Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15.0, 10.0, 15.0, 10.0)
            };
            grid.Children.Add(headerBorder);

            StackPanel headerContent = new StackPanel { Orientation = Orientation.Horizontal };
            headerBorder.Child = headerContent;

            headerContent.Children.Add(new TextBlock
            {
                Text = "ОБЪЕДИНЕНИЕ ОТВЕРСТИЙ " + titleSuffix.ToUpper(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16.0,
                VerticalAlignment = VerticalAlignment.Center
            });

            Border contentBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1.0),
                CornerRadius = new CornerRadius(5.0),
                Padding = new Thickness(15.0),
                Margin = new Thickness(15.0, 10.0, 15.0, 10.0)
            };
            Grid.SetRow(contentBorder, 1);
            grid.Children.Add(contentBorder);

            Grid grid2 = new Grid();
            grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200.0) });
            grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            contentBorder.Child = grid2;

            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(TextBox.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36))));
            style.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            style.Setters.Add(new Setter(TextBox.CaretBrushProperty, Brushes.White));
            style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(5, 2, 5, 2)));

            Style style2 = new Style(typeof(ComboBox));
            style2.Setters.Add(new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36))));
            style2.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            style2.Setters.Add(new Setter(ComboBox.BorderBrushProperty, borderBrush));

            Style style3 = new Style(typeof(ComboBoxItem));
            style3.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 36))));
            style3.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(ComboBoxItem), style3);

            Style style4 = new Style(typeof(CheckBox));
            style4.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brushes.White));

            Style roundedButtonStyle = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5.0));
            borderFactory.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.0));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(presenter);
            template.VisualTree = borderFactory;
            roundedButtonStyle.Setters.Add(new Setter(Button.TemplateProperty, template));

            int num = 0;
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            this.AddSectionHeader(grid2, "ВАРИАНТ 1: ВЫБРАТЬ ВРУЧНУЮ НА ВИДЕ", num);
            num++;

            Button button = new Button
            {
                Content = "Выбрать элементы для объединения",
                Height = 35.0,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0.0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
                Style = roundedButtonStyle
            };
            LinearGradientBrush normalGrad = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(255, 46, 91), System.Windows.Media.Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(255, 80, 118), System.Windows.Media.Color.FromRgb(210, 20, 60), 90.0);
            this.ApplyButtonStyles(button, normalGrad, hoverGrad, roundedButtonStyle);
            button.Click += new RoutedEventHandler(this.SelectBtn_Click);
            Grid.SetRow(button, num);
            Grid.SetColumnSpan(button, 2);
            grid2.Children.Add(button);
            num++;

            this.AddSectionHeader(grid2, "ВАРИАНТ 2: АВТОМАТИЧЕСКОЕ ОБЪЕДИНЕНИЕ ВСЕХ", num);
            num++;

            this.AddLabel(grid2, "Поиск семейства:", num);
            this._searchBox = new TextBox
            {
                Style = style,
                Margin = new Thickness(0.0, 0.0, 0.0, 5.0),
                Height = 24.0
            };
            Grid.SetRow(this._searchBox, num);
            Grid.SetColumn(this._searchBox, 1);
            grid2.Children.Add(this._searchBox);
            num++;

            this.AddLabel(grid2, "Семейство для объединения:", num);
            this._comboSourceFamily = new ComboBox
            {
                Style = style2,
                Margin = new Thickness(0.0, 0.0, 0.0, 5.0),
                Height = 26.0
            };
            UiThemeHelper.StyleComboBoxDark(this._comboSourceFamily, panelBg, borderBrush);

            this._allItems = symbols.Select(s => new SymbolItem
            {
                Symbol = s,
                Display = s.Family.Name + " : " + s.Name
            }).ToList();
            this._comboSourceFamily.ItemsSource = this._allItems;

            SymbolItem symbolItem = this._allItems.FirstOrDefault(i =>
                i.Display.ToLower().Contains("отверстие") || i.Display.ToLower().Contains("opening"));
            if (symbolItem != null)
            {
                this._comboSourceFamily.SelectedItem = symbolItem;
            }
            else if (this._allItems.Count > 0)
            {
                this._comboSourceFamily.SelectedIndex = 0;
            }

            Grid.SetRow(this._comboSourceFamily, num);
            Grid.SetColumn(this._comboSourceFamily, 1);
            grid2.Children.Add(this._comboSourceFamily);
            num++;

            this._searchBox.TextChanged += (s, e) =>
            {
                string text = this._searchBox.Text.ToLower();
                List<SymbolItem> itemsSource = string.IsNullOrWhiteSpace(text) ? this._allItems : (
                    from i in this._allItems
                    where i.Display.ToLower().Contains(text)
                    select i).ToList();
                this._comboSourceFamily.ItemsSource = itemsSource;
                if (this._comboSourceFamily.Items.Count > 0)
                {
                    this._comboSourceFamily.SelectedIndex = 0;
                }
            };

            this.AddLabel(grid2, "Область поиска:", num);
            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
            };
            this._chkCurrentView = new CheckBox
            {
                Style = style4,
                Content = "На текущем виде",
                IsChecked = true,
                Margin = new Thickness(0.0, 0.0, 15.0, 0.0)
            };
            this._chkAllProject = new CheckBox
            {
                Style = style4,
                Content = "Во всем проекте",
                IsChecked = false
            };
            this._chkCurrentView.Checked += (s, ev) =>
            {
                if (this._chkAllProject != null)
                {
                    this._chkAllProject.IsChecked = false;
                }
            };
            this._chkAllProject.Checked += (s, ev) =>
            {
                if (this._chkCurrentView != null)
                {
                    this._chkCurrentView.IsChecked = false;
                }
            };
            stackPanel.Children.Add(this._chkCurrentView);
            stackPanel.Children.Add(this._chkAllProject);
            Grid.SetRow(stackPanel, num);
            Grid.SetColumn(stackPanel, 1);
            grid2.Children.Add(stackPanel);
            num++;

            this.AddLabel(grid2, "Расстояние менее (мм):", num);
            this._txtMergeDistance = new TextBox
            {
                Style = style,
                Text = "200",
                Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
                Height = 24.0
            };
            Grid.SetRow(this._txtMergeDistance, num);
            Grid.SetColumn(this._txtMergeDistance, 1);
            grid2.Children.Add(this._txtMergeDistance);
            num++;

            Button button2 = new Button
            {
                Content = "Объединить все по расстоянию",
                Height = 35.0,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0.0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
                Style = roundedButtonStyle
            };
            SolidColorBrush normalBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 120, 240));
            SolidColorBrush hoverBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 140, 255));
            this.ApplyFlatButtonStyles(button2, normalBrush, hoverBrush, roundedButtonStyle);
            button2.Click += new RoutedEventHandler(this.BatchBtn_Click);
            Grid.SetRow(button2, num);
            Grid.SetColumnSpan(button2, 2);
            grid2.Children.Add(button2);
            num++;

            Grid grid3 = new Grid
            {
                Margin = new Thickness(15.0, 10.0, 15.0, 10.0)
            };
            grid3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120.0) });
            Grid.SetRow(grid3, 2);
            grid.Children.Add(grid3);

            Button button3 = new Button
            {
                Content = "Отмена",
                Height = 35.0,
                IsCancel = true,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0.0),
                Cursor = Cursors.Hand,
                Style = roundedButtonStyle
            };
            SolidColorBrush normalBrush2 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 66));
            SolidColorBrush hoverBrush2 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 78, 88));
            this.ApplyFlatButtonStyles(button3, normalBrush2, hoverBrush2, roundedButtonStyle);
            button3.Click += (s, ev) =>
            {
                this.ResultAction = MergeAction.Cancel;
                this.Close();
            };
            Grid.SetColumn(button3, 1);
            grid3.Children.Add(button3);
        }

        private TextBlock AddLabel(Grid grid, string text, int row)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0.0, 0.0, 10.0, 5.0)
            };
            Grid.SetRow(textBlock, row);
            Grid.SetColumn(textBlock, 0);
            grid.Children.Add(textBlock);
            return textBlock;
        }

        private void AddSectionHeader(Grid grid, string text, int row)
        {
            Border border = new Border
            {
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 70)),
                BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
                Padding = new Thickness(0.0, 10.0, 0.0, 5.0),
                Margin = new Thickness(0.0, 5.0, 0.0, 10.0)
            };
            TextBlock child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 46, 91)),
                FontWeight = FontWeights.Bold,
                FontSize = 12.0
            };
            border.Child = child;
            Grid.SetRow(border, row);
            Grid.SetColumnSpan(border, 2);
            grid.Children.Add(border);
        }

        private void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            this.ResultAction = MergeAction.ManualPick;
            this.DialogResult = true;
            this.Close();
        }

        private void BatchBtn_Click(object sender, RoutedEventArgs e)
        {
            SymbolItem symbolItem = this._comboSourceFamily.SelectedItem as SymbolItem;
            if (symbolItem == null)
            {
                MessageBox.Show("Выберите семейство для объединения в Варианте 2.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            this.SourceSymbol = symbolItem.Symbol;
            this.SelectedSymbol = symbolItem.Symbol;
            string widthParam;
            string heightParam;
            string depthParam;
            OpeningsRunner.AutoDetectParamNames(this._doc, this.SourceSymbol, null, null, null, out widthParam, out heightParam, out depthParam);
            this.WidthParam = widthParam;
            this.HeightParam = heightParam;
            this.DepthParam = depthParam;
            this.OffsetMm = 0.0;
            this.DepthOffsetMm = 0.0;
            double num;
            if (double.TryParse(this._txtMergeDistance.Text.Replace('.', ','), out num) && num >= 0.0)
            {
                this.MergeDistanceMm = num;
                this.UseCurrentViewOnly = this._chkCurrentView?.IsChecked == true;
                this.ResultAction = MergeAction.BatchMerge;
                this.DialogResult = true;
                this.Close();
                return;
            }
            MessageBox.Show("Некорректное значение расстояния.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private void ApplyButtonStyles(Button btn, Brush normalGrad, Brush hoverGrad, Style baseStyle)
        {
            btn.Background = normalGrad;
            btn.MouseEnter += (s, e) => btn.Background = hoverGrad;
            btn.MouseLeave += (s, e) => btn.Background = normalGrad;
        }

        private void ApplyFlatButtonStyles(Button btn, Brush normalBrush, Brush hoverBrush, Style baseStyle)
        {
            btn.Background = normalBrush;
            btn.MouseEnter += (s, e) => btn.Background = hoverBrush;
            btn.MouseLeave += (s, e) => btn.Background = normalBrush;
        }
    }

    // ========================================================================
    // Регистрация
    // ========================================================================
    [Transaction(TransactionMode.Manual)]
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "MEP Инструменты";
            try { application.CreateRibbonTab(tabName); } catch { }

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Отверстия");

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath);

            PushButtonData wallBtn = new PushButtonData(
                "DuctWallOpenings",
                "Отверстия\nв стенах",
                assemblyPath,
                "DuctWallOpenings.DuctWallOpeningsCommand")
            {
                ToolTip = "Расстановка отверстий в стенах для воздуховодов, труб и лотков",
                LongDescription = "Поддержка связанных моделей, выбор семейства и параметров, фильтры по виду/выделению."
            };

            PushButtonData floorBtn = new PushButtonData(
                "DuctFloorOpenings",
                "Отверстия\nв полах",
                assemblyPath,
                "DuctWallOpenings.DuctFloorOpeningsCommand")
            {
                ToolTip = "Расстановка отверстий в полах/перекрытиях для вертикальных воздуховодов, труб и лотков",
                LongDescription = "Расставляет отверстия в местах прохождения вертикальных элементов сквозь полы."
            };

            string wallIcon = Path.Combine(assemblyDir, "wall_icon.png");
            string floorIcon = Path.Combine(assemblyDir, "floor_icon.png");
            TrySetIcon(wallBtn, wallIcon);
            TrySetIcon(floorBtn, floorIcon);

            panel.AddItem(wallBtn);
            panel.AddItem(floorBtn);

            return Result.Succeeded;
        }

        private void TrySetIcon(PushButtonData btn, string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                btn.LargeImage = img;
                btn.Image = img;
            }
            catch { }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}