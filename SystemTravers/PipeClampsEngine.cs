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

    public class PipeClampsResult
    {
        public int ClampsPlaced { get; set; }
        public int PipesProcessed { get; set; }
        public List<string> Logs { get; set; } = new List<string>();
    }

    public static class PipeClampsEngine
    {
        public static List<PipeDiameterInfo> GetVerticalPipeDiameters(Document doc)
        {
            var dict = new Dictionary<int, List<Pipe>>();

            var pipes = new FilteredElementCollector(doc)
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

        public static PipeClampsResult RunPlacement(Document doc, List<PipeDiameterInfo> diameterSettings)
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

            var pipes = new FilteredElementCollector(doc)
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
                        FamilyInstance clampInst;

                        if (level != null)
                        {
                            clampInst = doc.Create.NewFamilyInstance(placementPoint, sym, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                        else
                        {
                            clampInst = doc.Create.NewFamilyInstance(placementPoint, sym, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }

                        if (clampInst != null)
                        {
                            countOnPipe++;
                            result.ClampsPlaced++;
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
    }
}
