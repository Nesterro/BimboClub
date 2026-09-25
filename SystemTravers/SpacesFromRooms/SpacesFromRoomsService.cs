using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using View = Autodesk.Revit.DB.View;

namespace BimboClub.SpacesFromRooms
{
    public class LinkOption
    {
        public RevitLinkInstance Instance { get; set; }
        public Document LinkDocument { get; set; }
        public string DisplayName { get; set; }
        public int RoomCount { get; set; }

        public override string ToString() => DisplayName;
    }

    public class LevelOption
    {
        public Level Level { get; set; }
        public string Name => Level?.Name ?? "Неизвестно";
        public double Elevation => Level?.Elevation ?? 0;
        public bool IsSelected { get; set; } = true;

        public override string ToString() => Name;
    }

    public class SpaceCreationSettings
    {
        public RevitLinkInstance SelectedLink { get; set; }
        public List<Level> SelectedLevels { get; set; } = new List<Level>();
        public bool CopyNameAndNumber { get; set; } = true;
        public bool CopyHeightsAndLimits { get; set; } = true;
        public bool EnableRoomBounding { get; set; } = true;
        public bool SkipExistingSpaces { get; set; } = true;
        public bool UpdateExistingSpaces { get; set; } = true;
    }

    public class SpaceCreationResult
    {
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    public static class SpacesFromRoomsService
    {
        /// <summary>
        /// Поиск всех связанных файлов, содержащих помещения
        /// </summary>
        public static List<LinkOption> GetLinksWithRooms(Document hostDoc)
        {
            var result = new List<LinkOption>();
            if (hostDoc == null) return result;

            var linkInstances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            foreach (var inst in linkInstances)
            {
                Document linkDoc = inst.GetLinkDocument();
                if (linkDoc == null) continue;

                int roomCount = new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .OfType<Room>()
                    .Count(r => r.Area > 0.001 && r.Location is LocationPoint);

                string name = inst.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = linkDoc.Title;
                }

                result.Add(new LinkOption
                {
                    Instance = inst,
                    LinkDocument = linkDoc,
                    DisplayName = $"{name} (Помещений: {roomCount})",
                    RoomCount = roomCount
                });
            }

            return result;
        }

        /// <summary>
        /// Получение уровней хост-модели
        /// </summary>
        public static List<LevelOption> GetHostLevels(Document hostDoc)
        {
            if (hostDoc == null) return new List<LevelOption>();

            return new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => new LevelOption { Level = l, IsSelected = true })
                .ToList();
        }

        /// <summary>
        /// Основной метод расстановки пространств по помещениям
        /// </summary>
        public static SpaceCreationResult CreateSpaces(Document hostDoc, SpaceCreationSettings settings)
        {
            var res = new SpaceCreationResult();
            if (hostDoc == null || settings?.SelectedLink == null)
            {
                res.Messages.Add("Не указана связь или документ проекта.");
                return res;
            }

            RevitLinkInstance linkInst = settings.SelectedLink;
            Document linkDoc = linkInst.GetLinkDocument();
            if (linkDoc == null)
            {
                res.Messages.Add("Связанный документ недоступен (возможно, не загружен).");
                return res;
            }

            // 1. Проверяем и включаем Room Bounding при необходимости
            if (settings.EnableRoomBounding)
            {
                EnsureRoomBounding(hostDoc, linkInst);
            }

            // 2. Определяем фазу хост-модели для пространств
            Phase hostPhase = ResolveHostPhase(hostDoc);

            // 3. Получаем трансформацию связи
            Transform transform = linkInst.GetTotalTransform();

            // 4. Сбор всех помещений из связанного файла
            var rooms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .OfType<Room>()
                .Where(r => r.Area > 0.001 && r.Location is LocationPoint)
                .ToList();

            if (rooms.Count == 0)
            {
                res.Messages.Add("В выбранной связи не найдено размещенных помещений с площадью > 0.");
                return res;
            }

            // Кэш соответствия уровней связи и уровней хоста
            var levelMap = new Dictionary<ElementId, Level>();
            var allHostLevels = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            // Фильтр выбранных уровней хоста (если задан)
            HashSet<ElementId> allowedLevelIds = settings.SelectedLevels != null && settings.SelectedLevels.Count > 0
                ? new HashSet<ElementId>(settings.SelectedLevels.Select(l => l.Id))
                : null;

            using (Transaction t = new Transaction(hostDoc, "Расстановка пространств по АР"))
            {
                t.Start();

                foreach (Room room in rooms)
                {
                    try
                    {
                        LocationPoint locPt = room.Location as LocationPoint;
                        if (locPt == null)
                        {
                            res.SkippedCount++;
                            continue;
                        }

                        // Координата точки помещения в хост-модели
                        XYZ hostPoint = transform.OfPoint(locPt.Point);

                        // Определяем уровень в хост-документе
                        if (room.Level == null)
                        {
                            res.SkippedCount++;
                            continue;
                        }

                        if (!levelMap.TryGetValue(room.Level.Id, out Level hostLevel) || hostLevel == null)
                        {
                            hostLevel = MatchLevel(room.Level, allHostLevels, transform);
                            levelMap[room.Level.Id] = hostLevel;
                        }

                        if (hostLevel == null)
                        {
                            res.SkippedCount++;
                            continue;
                        }

                        // Если уровень не входит в список выбранных пользователем, пропускаем
                        if (allowedLevelIds != null && !allowedLevelIds.Contains(hostLevel.Id))
                        {
                            res.SkippedCount++;
                            continue;
                        }

                        // Проверяем, существует ли уже пространство в этой точке
                        Space existingSpace = hostDoc.GetSpaceAtPoint(hostPoint, hostPhase);

                        if (existingSpace != null)
                        {
                            if (settings.UpdateExistingSpaces && settings.CopyNameAndNumber)
                            {
                                UpdateSpaceParameters(existingSpace, room, settings.CopyHeightsAndLimits, allHostLevels, transform);
                                res.UpdatedCount++;
                            }
                            else
                            {
                                res.SkippedCount++;
                            }
                            continue;
                        }

                        // Создание нового пространства
                        UV uv = new UV(hostPoint.X, hostPoint.Y);
                        Space newSpace = null;

                        try
                        {
                            if (hostPhase != null)
                            {
                                newSpace = hostDoc.Create.NewSpace(hostLevel, hostPhase, uv);
                            }
                        }
                        catch { }

                        if (newSpace == null)
                        {
                            try
                            {
                                newSpace = hostDoc.Create.NewSpace(hostLevel, uv);
                            }
                            catch (Exception exCreate)
                            {
                                Logger.Log($"Ошибка NewSpace на уровне {hostLevel.Name}: {exCreate.Message}", "WARN");
                            }
                        }

                        if (newSpace == null)
                        {
                            res.ErrorCount++;
                            continue;
                        }

                        // Назначение параметров
                        if (settings.CopyNameAndNumber || settings.CopyHeightsAndLimits)
                        {
                            UpdateSpaceParameters(newSpace, room, settings.CopyHeightsAndLimits, allHostLevels, transform);
                        }

                        res.CreatedCount++;
                    }
                    catch (Exception exItem)
                    {
                        res.ErrorCount++;
                        Logger.Log($"Ошибка обработки помещения {room.Number} {room.Name}: {exItem.Message}", "WARN");
                    }
                }

                t.Commit();
            }

            return res;
        }

        private static void UpdateSpaceParameters(Space space, Room room, bool copyHeights, List<Level> allHostLevels, Transform transform)
        {
            if (space == null || room == null) return;

            // 1. Имя
            string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? room.Name;
            if (!string.IsNullOrEmpty(roomName))
            {
                try
                {
                    space.Name = roomName;
                }
                catch
                {
                    space.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set(roomName);
                }
            }

            // 2. Номер
            string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? room.Number;
            if (!string.IsNullOrEmpty(roomNumber))
            {
                try
                {
                    space.Number = roomNumber;
                }
                catch
                {
                    space.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.Set(roomNumber);
                }
            }

            // 3. Высотные отметки
            if (copyHeights)
            {
                try
                {
                    space.BaseOffset = room.BaseOffset;
                }
                catch { }

                try
                {
                    space.LimitOffset = room.LimitOffset;
                }
                catch { }

                if (room.UpperLimit != null)
                {
                    Level hostUpper = MatchLevel(room.UpperLimit, allHostLevels, transform);
                    if (hostUpper != null)
                    {
                        try
                        {
                            space.UpperLimit = hostUpper;
                        }
                        catch { }
                    }
                }
            }

            // 4. Популярные общие параметры (ADSK)
            CopyCommonParam(room, space, "ADSK_Номер помещения");
            CopyCommonParam(room, space, "ADSK_Наименование помещения");
            CopyCommonParam(room, space, "ADSK_Категория помещения");
            CopyCommonParam(room, space, "Помещение_Номер");
            CopyCommonParam(room, space, "Помещение_Имя");
        }

        private static void CopyCommonParam(Room room, Space space, string paramName)
        {
            try
            {
                Parameter pRoom = room.LookupParameter(paramName);
                Parameter pSpace = space.LookupParameter(paramName);

                if (pRoom != null && pSpace != null && !pSpace.IsReadOnly && pRoom.HasValue)
                {
                    if (pRoom.StorageType == StorageType.String)
                    {
                        pSpace.Set(pRoom.AsString());
                    }
                    else if (pRoom.StorageType == StorageType.Integer)
                    {
                        pSpace.Set(pRoom.AsInteger());
                    }
                    else if (pRoom.StorageType == StorageType.Double)
                    {
                        pSpace.Set(pRoom.AsDouble());
                    }
                }
            }
            catch { }
        }

        private static Level MatchLevel(Level linkedLevel, List<Level> hostLevels, Transform transform)
        {
            if (linkedLevel == null || hostLevels == null || hostLevels.Count == 0) return null;

            // 1. По точному имени
            Level matchByName = hostLevels.FirstOrDefault(l =>
                l.Name.Trim().Equals(linkedLevel.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchByName != null) return matchByName;

            // 2. По высотной отметке с учетом смещения связи
            double targetElevation = linkedLevel.Elevation + transform.Origin.Z;
            Level matchByElev = hostLevels
                .OrderBy(l => Math.Abs(l.Elevation - targetElevation))
                .FirstOrDefault();

            if (matchByElev != null && Math.Abs(matchByElev.Elevation - targetElevation) < 1.0) // погрешность до 300 мм
            {
                return matchByElev;
            }

            return matchByElev ?? hostLevels.FirstOrDefault();
        }

        private static Phase ResolveHostPhase(Document doc)
        {
            try
            {
                ElementId activeViewPhaseId = doc.ActiveView?.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId();
                if (activeViewPhaseId != null && activeViewPhaseId != ElementId.InvalidElementId)
                {
                    Phase p = doc.GetElement(activeViewPhaseId) as Phase;
                    if (p != null) return p;
                }
            }
            catch { }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .LastOrDefault();
        }

        private static void EnsureRoomBounding(Document doc, RevitLinkInstance linkInst)
        {
            try
            {
                Parameter p = linkInst.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
                if (p != null && !p.IsReadOnly && p.AsInteger() == 0)
                {
                    using (Transaction t = new Transaction(doc, "Включение границы помещений связи"))
                    {
                        t.Start();
                        p.Set(1);
                        t.Commit();
                    }
                    return;
                }

                Element linkType = doc.GetElement(linkInst.GetTypeId());
                if (linkType != null)
                {
                    Parameter pType = linkType.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
                    if (pType != null && !pType.IsReadOnly && pType.AsInteger() == 0)
                    {
                        using (Transaction t = new Transaction(doc, "Включение границы помещений связи"))
                        {
                            t.Start();
                            pType.Set(1);
                            t.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Warning: не удалось включить Room Bounding связи: {ex.Message}", "WARN");
            }
        }
    }
}
