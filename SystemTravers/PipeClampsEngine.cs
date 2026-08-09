using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace BimboClub.PipeClamps
{
    public class PipeDiameterInfo
    {
        public double DiameterMm { get; set; }
        public int PipeCount { get; set; }
        public FamilySymbol SelectedClampSymbol { get; set; }
        public double StepMm { get; set; } = 1500;
        public double OffsetMm { get; set; } = 300;

        public string DisplayName => $"Ø{Math.Round(DiameterMm, 0)} мм ({PipeCount} шт.)";
    }

    public class PipeClampsPlacementOptions
    {
        public bool ActiveViewOnly { get; set; } = false;

        public bool CopyParam1 { get; set; } = true;
        public string SourceParam1 { get; set; } = "ADSK_Номер стояка";
        public string TargetParam1 { get; set; } = "ADSK_Номер стояка";

        public bool CopyParam2 { get; set; } = true;
        public string SourceParam2 { get; set; } = "ADSK_Секция";
        public string TargetParam2 { get; set; } = "ADSK_Секция";

        public double RotateXDeg { get; set; } = 0;
        public double RotateYDeg { get; set; } = 0;
        public double RotateZDeg { get; set; } = 0;
    }

    public class PipeClampsResult
    {
        public int ClampsPlaced { get; set; }
        public int PipesProcessed { get; set; }
        public List<string> Logs { get; set; } = new List<string>();
    }

    public static class PipeClampsEngine
    {
        public static List<PipeDiameterInfo> GetVerticalPipeDiameters(Document doc, bool activeViewOnly = false)
        {
            var dict = new Dictionary<int, List<Pipe>>();

            FilteredElementCollector collector = (activeViewOnly && doc?.ActiveView != null)
                ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                : new FilteredElementCollector(doc);

            var pipes = collector
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsNotElementType()
                .Cast<Pipe>()
                .ToList();

            foreach (var pipe in pipes)
            {
                if (IsVerticalPipe(pipe))
                {
                    double diamMm = GetPipeDiameterMm(pipe);
                    int diamKey = (int)Math.Round(diamMm);

                    if (!dict.ContainsKey(diamKey))
                    {
                        dict[diamKey] = new List<Pipe>();
                    }
                    dict[diamKey].Add(pipe);
                }
            }

            var result = new List<PipeDiameterInfo>();
            foreach (var kvp in dict.OrderBy(k => k.Key))
            {
                result.Add(new PipeDiameterInfo
                {
                    DiameterMm = kvp.Key,
                    PipeCount = kvp.Value.Count,
                    StepMm = 1500,
                    OffsetMm = 300
                });
            }

            return result;
        }

        public static List<FamilySymbol> GetClampSymbols(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(fs => fs.FamilyName)
                .ThenBy(fs => fs.Name)
                .ToList();
        }

        public static PipeClampsResult RunPlacement(Document doc, List<PipeDiameterInfo> diameterSettings, PipeClampsPlacementOptions options = null)
        {
            var result = new PipeClampsResult();

            if (doc == null || diameterSettings == null || diameterSettings.Count == 0)
            {
                result.Logs.Add("Нет настроек для обработки.");
                return result;
            }

            var settingsMap = diameterSettings
                .Where(s => s.SelectedClampSymbol != null)
                .ToDictionary(s => (int)Math.Round(s.DiameterMm), s => s);

            if (settingsMap.Count == 0)
            {
                result.Logs.Add("Не выбран ни один хомут для расстановки.");
                return result;
            }

            FilteredElementCollector collector = (options != null && options.ActiveViewOnly && doc.ActiveView != null)
                ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                : new FilteredElementCollector(doc);

            var pipes = collector
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsNotElementType()
                .Cast<Pipe>()
                .ToList();

            foreach (var pipe in pipes)
            {
                if (!IsVerticalPipe(pipe)) continue;

                double diamMm = GetPipeDiameterMm(pipe);
                int diamKey = (int)Math.Round(diamMm);

                if (!settingsMap.TryGetValue(diamKey, out var setting)) continue;

                FamilySymbol sym = setting.SelectedClampSymbol;
                if (sym == null) continue;

                if (!sym.IsActive)
                {
                    sym.Activate();
                    doc.Regenerate();
                }

                // Извлечение значений параметров стояка
                string valParam1 = null;
                if (options != null && options.CopyParam1 && !string.IsNullOrEmpty(options.SourceParam1))
                {
                    valParam1 = GetParameterStringValue(pipe, options.SourceParam1);
                }

                string valParam2 = null;
                if (options != null && options.CopyParam2 && !string.IsNullOrEmpty(options.SourceParam2))
                {
                    valParam2 = GetParameterStringValue(pipe, options.SourceParam2);
                }

                if (pipe.Location is LocationCurve locCurve && locCurve.Curve is Line line)
                {
                    XYZ p0 = line.GetEndPoint(0);
                    XYZ p1 = line.GetEndPoint(1);

                    // Убедимся, что p0 — нижняя точка
                    if (p0.Z > p1.Z)
                    {
                        XYZ temp = p0;
                        p0 = p1;
                        p1 = temp;
                    }

                    double totalHeightFeet = p1.Z - p0.Z;
                    double stepFeet = setting.StepMm / 304.8;
                    double offsetFeet = setting.OffsetMm / 304.8;

                    if (stepFeet <= 0.1) stepFeet = 1500 / 304.8;

                    int countOnPipe = 0;
                    for (double z = offsetFeet; z <= totalHeightFeet - 0.2; z += stepFeet)
                    {
                        XYZ placementPoint = new XYZ(p0.X, p0.Y, p0.Z + z);
                        Level level = doc.GetElement(pipe.LevelId) as Level;

                        FamilyInstance clampInst = null;

                        // Сначала пробуем свободной точкой (без жесткой привязки плоскости уровня),
                        // чтобы Revit разрешал свободный 3D поворот вокруг любых осей без смещения по плоскости
                        try
                        {
                            clampInst = doc.Create.NewFamilyInstance(placementPoint, sym, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                        catch { }

                        if (clampInst == null && level != null)
                        {
                            try
                            {
                                clampInst = doc.Create.NewFamilyInstance(placementPoint, sym, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                            catch { }
                        }

                        if (clampInst != null)
                        {
                            countOnPipe++;
                            result.ClampsPlaced++;

                            // 1. Копирование параметров из трубы стояка в хомут
                            if (options != null)
                            {
                                if (!string.IsNullOrEmpty(valParam1) && options.CopyParam1)
                                {
                                    string target1 = !string.IsNullOrEmpty(options.TargetParam1) ? options.TargetParam1 : options.SourceParam1;
                                    SetParameterValue(clampInst, target1, valParam1);
                                }

                                if (!string.IsNullOrEmpty(valParam2) && options.CopyParam2)
                                {
                                    string target2 = !string.IsNullOrEmpty(options.TargetParam2) ? options.TargetParam2 : options.SourceParam2;
                                    SetParameterValue(clampInst, target2, valParam2);
                                }
                            }

                            // 2. Выполнение 3D вращений вокруг осей элемента
                            if (options != null)
                            {
                                XYZ currentOrigin = (clampInst.Location is LocationPoint lp0) ? lp0.Point : placementPoint;
                                Transform tr = clampInst.GetTransform();

                                // Вращение по оси Z (вокруг стояка)
                                if (Math.Abs(options.RotateZDeg) > 0.001)
                                {
                                    try
                                    {
                                        Line axisZ = Line.CreateBound(currentOrigin, currentOrigin + tr.BasisZ);
                                        ElementTransformUtils.RotateElement(doc, clampInst.Id, axisZ, options.RotateZDeg * Math.PI / 180.0);
                                        tr = clampInst.GetTransform();
                                        if (clampInst.Location is LocationPoint lpZ) currentOrigin = lpZ.Point;
                                    }
                                    catch { }
                                }

                                // Вращение по локальной оси X элемента
                                if (Math.Abs(options.RotateXDeg) > 0.001)
                                {
                                    try
                                    {
                                        Line axisX = Line.CreateBound(currentOrigin, currentOrigin + tr.BasisX);
                                        ElementTransformUtils.RotateElement(doc, clampInst.Id, axisX, options.RotateXDeg * Math.PI / 180.0);
                                        tr = clampInst.GetTransform();
                                        if (clampInst.Location is LocationPoint lpX) currentOrigin = lpX.Point;
                                    }
                                    catch { }
                                }

                                // Вращение по локальной оси Y элемента
                                if (Math.Abs(options.RotateYDeg) > 0.001)
                                {
                                    try
                                    {
                                        Line axisY = Line.CreateBound(currentOrigin, currentOrigin + tr.BasisY);
                                        ElementTransformUtils.RotateElement(doc, clampInst.Id, axisY, options.RotateYDeg * Math.PI / 180.0);
                                    }
                                    catch { }
                                }
                            }

                            // 3. ПРИНУДИТЕЛЬНАЯ ФИКСАЦИЯ И ВОЗВРАТ НА ОСЬ ТРУБЫ (ПОЗОВОЛЯЕТ ИЗБЕЖАТЬ УЛЕТАНИЯ И СМЕЩЕНИЯ В СТОРОНУ)
                            if (clampInst.Location is LocationPoint lpFinal)
                            {
                                XYZ finalPos = lpFinal.Point;
                                XYZ moveDelta = placementPoint - finalPos;
                                if (moveDelta.GetLength() > 0.0001)
                                {
                                    ElementTransformUtils.MoveElement(doc, clampInst.Id, moveDelta);
                                }
                            }

                            // 4. Привязка уровня для спецификации (если применимо)
                            if (level != null)
                            {
                                try
                                {
                                    Parameter pLevel = clampInst.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                                    if (pLevel != null && !pLevel.IsReadOnly)
                                    {
                                        pLevel.Set(level.Id);
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    if (countOnPipe > 0)
                    {
                        result.PipesProcessed++;
                    }
                }
            }

            result.Logs.Add($"Обработано стояков: {result.PipesProcessed}");
            result.Logs.Add($"Размещено хомутов: {result.ClampsPlaced}");

            return result;
        }

        private static bool IsVerticalPipe(Pipe pipe)
        {
            if (pipe.Location is LocationCurve locCurve && locCurve.Curve is Line line)
            {
                XYZ dir = line.Direction.Normalize();
                return Math.Abs(dir.Z) > 0.95;
            }
            return false;
        }

        private static double GetPipeDiameterMm(Pipe pipe)
        {
            Parameter pDiam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (pDiam != null && pDiam.HasValue)
            {
                return pDiam.AsDouble() * 304.8;
            }
            return pipe.Diameter * 304.8;
        }

        private static string GetParameterStringValue(Element elem, string paramName)
        {
            if (elem == null || string.IsNullOrEmpty(paramName)) return null;

            Parameter p = elem.LookupParameter(paramName);
            if (p == null || !p.HasValue) return null;

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString();
                case StorageType.Integer:
                    return p.AsInteger().ToString();
                case StorageType.Double:
                    return p.AsValueString() ?? p.AsDouble().ToString("F2");
                case StorageType.ElementId:
                    return p.AsElementId()?.ToString();
                default:
                    return null;
            }
        }

        private static void SetParameterValue(Element elem, string paramName, string valStr)
        {
            if (elem == null || string.IsNullOrEmpty(paramName) || valStr == null) return;

            Parameter p = elem.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return;

            if (p.StorageType == StorageType.String)
            {
                p.Set(valStr);
            }
            else if (p.StorageType == StorageType.Integer && int.TryParse(valStr, out int intVal))
            {
                p.Set(intVal);
            }
            else if (p.StorageType == StorageType.Double && double.TryParse(valStr, out double dblVal))
            {
                p.Set(dblVal);
            }
        }
    }
}
