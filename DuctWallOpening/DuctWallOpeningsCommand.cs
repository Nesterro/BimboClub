using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
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
                List<MEPCurve> mepElements = CollectMepCurves(
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

                    foreach (MEPCurve mep in mepElements)
                    {
                        try
                        {
                            if (mep == null) continue;
                            LocationCurve lc = mep.Location as LocationCurve;
                            if (lc == null || lc.Curve == null) continue;
                            Line mepLine = lc.Curve as Line;
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
                                openingWidth = diameter + offset;
                                openingHeight = diameter + offset;
                            }
                            else
                            {
                                openingWidth = width + offset;
                                openingHeight = height + offset;
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

                                        if (!IsMepInsideWallHeight(mep, wall, tr)) continue;

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

        private static List<MEPCurve> CollectMepCurves(Document doc,
            bool useDucts, bool usePipes, bool useTrays,
            bool selectedOnly, bool currentViewOnly,
            List<MEPCurve> preselected)
        {
            var result = new List<MEPCurve>();

            if (selectedOnly)
            {
                foreach (var m in preselected)
                {
                    if (m is Duct && useDucts) result.Add(m);
                    else if (m is Pipe && usePipes) result.Add(m);
                    else if (m is CableTray && useTrays) result.Add(m);
                }
                return result;
            }

            ElementId viewId = currentViewOnly ? doc.ActiveView.Id : null;

            if (useDucts)
            {
                var c = viewId != null
                    ? new FilteredElementCollector(doc, viewId)
                    : new FilteredElementCollector(doc);
                result.AddRange(c.OfClass(typeof(Duct)).Cast<MEPCurve>());
            }
            if (usePipes)
            {
                var c = viewId != null
                    ? new FilteredElementCollector(doc, viewId)
                    : new FilteredElementCollector(doc);
                result.AddRange(c.OfClass(typeof(Pipe)).Cast<MEPCurve>());
            }
            if (useTrays)
            {
                var c = viewId != null
                    ? new FilteredElementCollector(doc, viewId)
                    : new FilteredElementCollector(doc);
                result.AddRange(c.OfClass(typeof(CableTray)).Cast<MEPCurve>());
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

        private static bool IsMepInsideWallHeight(MEPCurve mep, Wall wall, Transform tr)
        {
            try
            {
                BoundingBoxXYZ bb = wall.get_BoundingBox(null);
                if (bb == null) return true;
                XYZ min = tr.OfPoint(bb.Min);
                XYZ max = tr.OfPoint(bb.Max);
                double zmin = Math.Min(min.Z, max.Z);
                double zmax = Math.Max(min.Z, max.Z);

                LocationCurve dlc = mep.Location as LocationCurve;
                Line dl = dlc.Curve as Line;
                double dz1 = dl.GetEndPoint(0).Z;
                double dz2 = dl.GetEndPoint(1).Z;
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

        public FamilySelectionWindow(Document doc, List<FamilySymbol> symbols, bool hasPreselection, string titleSuffix)
        {
            _doc = doc;
            Title = "Параметры расстановки отверстий " + titleSuffix;
            Width = 620;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            Grid grid = new Grid { Margin = new Thickness(15) };
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
            _chkDucts = new CheckBox { Content = "Воздуховоды", IsChecked = true, Margin = new Thickness(0, 0, 12, 0) };
            _chkPipes = new CheckBox { Content = "Трубы", IsChecked = true, Margin = new Thickness(0, 0, 12, 0) };
            _chkTrays = new CheckBox { Content = "Лотки", IsChecked = true };
            typesPanel.Children.Add(_chkDucts);
            typesPanel.Children.Add(_chkPipes);
            typesPanel.Children.Add(_chkTrays);
            Grid.SetRow(typesPanel, row); Grid.SetColumn(typesPanel, 1);
            grid.Children.Add(typesPanel);
            row++;

            AddLabel(grid, "Поиск:", row);
            _searchBox = new TextBox { Margin = new Thickness(0, 0, 0, 5), Padding = new Thickness(3) };
            Grid.SetRow(_searchBox, row); Grid.SetColumn(_searchBox, 1);
            grid.Children.Add(_searchBox);
            row++;

            AddLabel(grid, "Семейство (тип):", row);
            _comboFamily = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
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
            _comboWidth = new ComboBox { Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(_comboWidth, row); Grid.SetColumn(_comboWidth, 1);
            grid.Children.Add(_comboWidth);
            row++;

            AddLabel(grid, "Параметр высоты:", row);
            _comboHeight = new ComboBox { Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(_comboHeight, row); Grid.SetColumn(_comboHeight, 1);
            grid.Children.Add(_comboHeight);
            row++;

            AddLabel(grid, "Параметр глубины (толщины):", row);
            _comboDepth = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_comboDepth, row); Grid.SetColumn(_comboDepth, 1);
            grid.Children.Add(_comboDepth);
            row++;

            AddLabel(grid, "Запас по ширине/высоте, мм:", row);
            _offsetBox = new TextBox { Text = "50", Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 5) };
            _offsetBox.ToolTip = "Прибавится к ширине и высоте отверстия";
            Grid.SetRow(_offsetBox, row); Grid.SetColumn(_offsetBox, 1);
            grid.Children.Add(_offsetBox);
            row++;

            AddLabel(grid, "Запас по глубине, мм:", row);
            _depthOffsetBox = new TextBox { Text = "50", Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 10) };
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
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_chkSelectedOnly, row); Grid.SetColumn(_chkSelectedOnly, 1);
            grid.Children.Add(_chkSelectedOnly);
            row++;

            _chkCurrentView = new CheckBox
            {
                Content = "Только элементы на текущем виде",
                IsChecked = false,
                Margin = new Thickness(0, 3, 0, 10),
                VerticalAlignment = VerticalAlignment.Center
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

            Button okBtn = new Button { Content = "OK", Width = 90, Height = 28, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            okBtn.Click += OkBtn_Click;
            buttons.Children.Add(okBtn);

            Button cancelBtn = new Button { Content = "Отмена", Width = 90, Height = 28, IsCancel = true };
            buttons.Children.Add(cancelBtn);

            grid.Children.Add(buttons);
            Content = grid;

            _comboFamily.SelectionChanged += (s, e) => UpdateParamLists();
            UpdateParamLists();
        }

        private void AddLabel(Grid grid, string text, int row)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
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