using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace BimboClub.HeatLoss
{
    public class HeatLossAnalysisOptions
    {
        public bool DeleteOldCubes { get; set; } = true;
        public bool CreateSpaces { get; set; } = true;
        public Phase SelectedPhase { get; set; }
        public SpatialElementBoundaryLocation BoundaryLocation { get; set; } = SpatialElementBoundaryLocation.Finish;
    }

    public class HeatLossResult
    {
        public int SpacesCreated { get; set; }
        public int CubesPlaced { get; set; }
        public int WarningsCount { get; set; }
        public List<string> Logs { get; set; } = new List<string>();
    }

    public static class HeatLossAnalyzer
    {
        public const string CUBE_FAMILY_NAME = "HeatLoss_Cube";

        public static HeatLossResult Run(Document doc, HeatLossAnalysisOptions options, Action<int, string> progressCallback = null)
        {
            var result = new HeatLossResult();

            if (doc == null)
            {
                result.Logs.Add("ОШИБКА: Документ Revit равен null.");
                return result;
            }

            // 1. Поиск семейства HeatLoss_Cube в проекте
            FamilySymbol defaultSymbol = FindOrCreateCubeSymbol(doc, result);
            if (defaultSymbol == null)
            {
                result.Logs.Add($"ОШИБКА: Семейство '{CUBE_FAMILY_NAME}' не найдено в проекте.");
                result.Logs.Add("Пожалуйста, загрузите семейство HeatLoss_Cube.rfa в проект перед запуском расчета.");
                return result;
            }

            if (!defaultSymbol.IsActive)
            {
                defaultSymbol.Activate();
            }

            // 2. Очистка старых кубиков при необходимости
            if (options.DeleteOldCubes)
            {
                DeleteExistingCubes(doc, result);
            }

            // 3. Сбор помещений (Rooms)
            var roomsCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0 && r.Location != null)
                .ToList();

            if (options.SelectedPhase != null)
            {
                roomsCollector = roomsCollector
                    .Where(r => r.get_Parameter(BuiltInParameter.ROOM_PHASE)?.AsElementId() == options.SelectedPhase.Id)
                    .ToList();
            }

            if (roomsCollector.Count == 0)
            {
                result.Logs.Add("ПРЕДУПРЕЖДЕНИЕ: В проекте не найдено замкнутых помещений (Rooms).");
                return result;
            }

            int totalRooms = roomsCollector.Count;
            result.Logs.Add($"Найдено помещений для анализа: {totalRooms}");

            // 4. Опции границ элементов
            var boundaryOpts = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = options.BoundaryLocation
            };

            int roomIndex = 0;
            foreach (Room room in roomsCollector)
            {
                roomIndex++;
                progressCallback?.Invoke((int)((double)roomIndex / totalRooms * 100), $"Обработка помещения: {room.Number} {room.Name}");

                // Анализ границ помещения (BoundarySegments)
                IList<IList<BoundarySegment>> boundaryLoops = room.GetBoundarySegments(boundaryOpts);
                if (boundaryLoops == null || boundaryLoops.Count == 0)
                {
                    result.WarningsCount++;
                    result.Logs.Add($"Помещение {room.Number} ({room.Name}) незамкнуто или не имеет контура — пропущено.");
                    continue;
                }

                Level level = room.Level;
                if (level == null) continue;

                // Точка центра помещения
                XYZ roomCenter = GetRoomCenter(room);

                // Создание Space (Пространства) при необходимости
                Space space = null;
                if (options.CreateSpaces)
                {
                    try
                    {
                        space = doc.Create.NewSpace(level, new UV(roomCenter.X, roomCenter.Y));
                        if (space != null)
                        {
                            space.Name = room.Name;
                            space.Number = room.Number;
                            result.SpacesCreated++;
                        }
                    }
                    catch
                    {
                        // Пространство могло уже существовать в этой точке
                    }
                }

                int cubeIndexInRoom = 0;

                foreach (var loop in boundaryLoops)
                {
                    foreach (BoundarySegment segment in loop)
                    {
                        Element hostElem = doc.GetElement(segment.ElementId);
                        if (hostElem == null) continue;

                        // Определение категории конструкции
                        long catIdVal = GetIdValue(hostElem.Category.Id);
                        BuiltInCategory cat = (BuiltInCategory)catIdVal;
                        string categoryName = GetCategoryDisplayName(cat);

                        bool isExternal = IsElementExternal(hostElem);

                        // Геометрия сегмента
                        Curve curve = segment.GetCurve();
                        double segmentLength = curve.Length;
                        double roomUnboundHeight = room.UnboundedHeight > 0 ? room.UnboundedHeight : 3.0 * 3.28084; // 3 м по умолчанию
                        double segmentAreaSqFt = segmentLength * roomUnboundHeight;

                        // Расчет нормали и азимута
                        XYZ normal = GetSegmentNormal(curve);
                        double azimuth = CalculateAzimuth(normal);

                        // Проверка на встройки в стене (Окна и Двери)
                        List<Element> inserts = new List<Element>();
                        if (hostElem is Wall wall)
                        {
                            IList<ElementId> insertIds = wall.FindInserts(true, false, false, false);
                            foreach (ElementId insId in insertIds)
                            {
                                Element insElem = doc.GetElement(insId);
                                if (insElem != null)
                                {
                                    long insCatVal = GetIdValue(insElem.Category.Id);
                                    if (insCatVal == (long)BuiltInCategory.OST_Doors || insCatVal == (long)BuiltInCategory.OST_Windows)
                                    {
                                        inserts.Add(insElem);
                                    }
                                }
                            }
                        }

                        double totalInsertsAreaSqFt = 0;

                        // Размещение кубиков для встроек (Двери / Окна)
                        foreach (Element insert in inserts)
                        {
                            long insCatVal = GetIdValue(insert.Category.Id);
                            string insCatName = insCatVal == (long)BuiltInCategory.OST_Doors ? "Дверь" : "Окно";
                            double insAreaSqFt = GetInsertAreaSqFt(insert);
                            totalInsertsAreaSqFt += insAreaSqFt;

                            XYZ insFacing = (insert as FamilyInstance)?.FacingOrientation ?? normal;
                            double insAzimuth = CalculateAzimuth(insFacing);

                            XYZ cubePos = GetCubePlacementPoint(roomCenter, cubeIndexInRoom++);
                            FamilySymbol insSymbol = GetSymbolForCategory(doc, insCatName) ?? defaultSymbol;

                            FamilyInstance insCube = doc.Create.NewFamilyInstance(cubePos, insSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            FillCubeParameters(insCube, GetIdValue(insert.Id), insCatName, insAreaSqFt * 0.09290304, insAzimuth, room.Name, room.Number, isExternal);
                            result.CubesPlaced++;
                        }

                        // Чистая площадь стены за вычетом встроек
                        double netAreaSqFt = Math.Max(0.1, segmentAreaSqFt - totalInsertsAreaSqFt);
                        double netAreaSqMeters = netAreaSqFt * 0.09290304;

                        // Размещение кубика для самой конструкции (Стена / Конструкция)
                        XYZ wallCubePos = GetCubePlacementPoint(roomCenter, cubeIndexInRoom++);
                        FamilySymbol wallSymbol = GetSymbolForCategory(doc, categoryName) ?? defaultSymbol;

                        FamilyInstance wallCube = doc.Create.NewFamilyInstance(wallCubePos, wallSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        FillCubeParameters(wallCube, GetIdValue(hostElem.Id), categoryName, netAreaSqMeters, azimuth, room.Name, room.Number, isExternal);
                        result.CubesPlaced++;
                    }
                }

                // Добавление горизонтальных ограждений (Пол и Покрытие / Перекрытие)
                ProcessHorizontalBoundary(doc, room, roomCenter, "Перекрытие", defaultSymbol, result, ref cubeIndexInRoom);
                ProcessHorizontalBoundary(doc, room, roomCenter, "Крыша", defaultSymbol, result, ref cubeIndexInRoom);
            }

            return result;
        }

        private static void ProcessHorizontalBoundary(Document doc, Room room, XYZ roomCenter, string catName, FamilySymbol defaultSymbol, HeatLossResult result, ref int cubeIndex)
        {
            double areaSqMeters = room.Area * 0.09290304;
            if (areaSqMeters <= 0) return;

            XYZ pos = GetCubePlacementPoint(roomCenter, cubeIndex++);
            FamilySymbol symbol = GetSymbolForCategory(doc, catName) ?? defaultSymbol;

            FamilyInstance cube = doc.Create.NewFamilyInstance(pos, symbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            FillCubeParameters(cube, GetIdValue(room.LevelId), catName, areaSqMeters, 0.0, room.Name, room.Number, catName == "Крыша");
            result.CubesPlaced++;
        }

        public static long GetIdValue(ElementId id)
        {
            if (id == null) return -1;
#if NET8_0_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static double CalculateAzimuth(XYZ normal)
        {
            double x = normal.X;
            double y = normal.Y;
            double len = Math.Sqrt(x * x + y * y);
            if (len < 1e-5) return 0.0; // Горизонтальный элемент

            x /= len;
            y /= len;

            // Atan2(x, y) дает угол от оси +Y (Север) по часовой стрелке к +X (Восток)
            double rad = Math.Atan2(x, y);
            double deg = rad * (180.0 / Math.PI);
            if (deg < 0) deg += 360.0;

            return Math.Round(deg, 1);
        }

        private static XYZ GetSegmentNormal(Curve curve)
        {
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            XYZ dir = (p1 - p0).Normalize();
            // Вектор перпендикуляра в плоскости XY
            return new XYZ(-dir.Y, dir.X, 0).Normalize();
        }

        private static XYZ GetRoomCenter(Room room)
        {
            if (room.Location is LocationPoint lp)
            {
                return lp.Point;
            }
            BoundingBoxXYZ bbox = room.get_BoundingBox(null);
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) * 0.5;
            }
            return XYZ.Zero;
        }

        private static XYZ GetCubePlacementPoint(XYZ center, int index)
        {
            // Стопка кубиков по вертикали с шагом 0.5 фута (около 15 см) в центре помещения
            return center + new XYZ(0, 0, index * 0.5);
        }

        private static bool IsElementExternal(Element elem)
        {
            Parameter pFunc = elem.get_Parameter(BuiltInParameter.FUNCTION_PARAM);
            if (pFunc != null)
            {
                int val = pFunc.AsInteger();
                return val == (int)WallFunction.Exterior;
            }
            return false;
        }

        private static double GetInsertAreaSqFt(Element insert)
        {
            double width = insert.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                           ?? insert.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsDouble()
                           ?? 3.0;
            double height = insert.get_Parameter(BuiltInParameter.DOOR_HEIGHT)?.AsDouble()
                            ?? insert.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)?.AsDouble()
                            ?? 6.5;
            return width * height;
        }

        private static string GetCategoryDisplayName(BuiltInCategory cat)
        {
            switch (cat)
            {
                case BuiltInCategory.OST_Walls: return "Стена";
                case BuiltInCategory.OST_Doors: return "Дверь";
                case BuiltInCategory.OST_Windows: return "Окно";
                case BuiltInCategory.OST_Floors: return "Перекрытие";
                case BuiltInCategory.OST_Roofs: return "Крыша";
                default: return "Ограждение";
            }
        }

        private static FamilySymbol FindOrCreateCubeSymbol(Document doc, HeatLossResult result)
        {
            var symbols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.FamilyName.Equals(CUBE_FAMILY_NAME, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return symbols.FirstOrDefault();
        }

        private static FamilySymbol GetSymbolForCategory(Document doc, string catName)
        {
            string symbolName = "Cube_Wall";
            if (catName.Contains("Дверь")) symbolName = "Cube_Door";
            else if (catName.Contains("Окно")) symbolName = "Cube_Window";
            else if (catName.Contains("Перекрытие") || catName.Contains("Пол")) symbolName = "Cube_Floor";
            else if (catName.Contains("Крыша")) symbolName = "Cube_Roof";

            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Name.Equals(symbolName, StringComparison.OrdinalIgnoreCase));
        }

        private static void DeleteExistingCubes(Document doc, HeatLossResult result)
        {
            var existingCubes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName.Equals(CUBE_FAMILY_NAME, StringComparison.OrdinalIgnoreCase))
                .Select(fi => fi.Id)
                .ToList();

            if (existingCubes.Count > 0)
            {
                doc.Delete(existingCubes);
                result.Logs.Add($"Удалено ранее размещенных кубиков: {existingCubes.Count}");
            }
        }

        private static void FillCubeParameters(FamilyInstance cube, long elementId, string category, double areaSqM, double azimuth, string roomName, string roomNumber, bool isExternal)
        {
            SetParam(cube, "HL_Element_Id", (int)elementId);
            SetParam(cube, "HL_Element_Category", category);
            SetParam(cube, "HL_Area", areaSqM);
            SetParam(cube, "HL_Orientation_Azimuth", azimuth);
            SetParam(cube, "HL_Room_Name", roomName ?? "");
            SetParam(cube, "HL_Room_Number", roomNumber ?? "");
            SetParam(cube, "HL_Is_External", isExternal ? 1 : 0);
        }

        private static void SetParam(Element elem, string paramName, object val)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return;

            if (val is double dVal)
            {
                if (p.StorageType == StorageType.Double) p.Set(dVal);
                else if (p.StorageType == StorageType.String) p.Set(dVal.ToString("F2"));
            }
            else if (val is int iVal)
            {
                if (p.StorageType == StorageType.Integer) p.Set(iVal);
                else if (p.StorageType == StorageType.String) p.Set(iVal.ToString());
            }
            else if (val is string sVal)
            {
                p.Set(sVal);
            }
        }
    }
}
