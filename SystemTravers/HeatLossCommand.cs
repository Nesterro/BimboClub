using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using View = Autodesk.Revit.DB.View;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class HeatLossCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View activeView = doc?.ActiveView;

            if (doc == null || activeView == null)
            {
                message = "Не найден активный вид или документ.";
                return Result.Failed;
            }

            try
            {
                Logger.Log("Запуск команды расчета теплопотерь.");

                // 1. Сбор помещений на активном виде (или во всем проекте)
                var rooms = new FilteredElementCollector(doc, activeView.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .Cast<SpatialElement>()
                    .ToList();

                if (rooms.Count == 0)
                {
                    rooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .Cast<SpatialElement>()
                        .ToList();
                }

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В проекте не найдено помещений (Rooms) для расчета теплопотерь.");
                    return Result.Cancelled;
                }

                // Строим список комнат для WPF UI
                List<RoomHeatLossItem> roomItems = new List<RoomHeatLossItem>();
                foreach (var r in rooms)
                {
                    double rawArea = r.get_Parameter(BuiltInParameter.ROOM_AREA)?.AsDouble() ?? 0;
                    double rawVolume = r.get_Parameter(BuiltInParameter.ROOM_VOLUME)?.AsDouble() ?? 0;

                    double area = rawArea * 0.092903;      // ft2 to m2
                    double volume = rawVolume * 0.0283168; // ft3 to m3

                    if (area > 0)
                    {
                        roomItems.Add(new RoomHeatLossItem
                        {
                            Number = r.Number,
                            Name = r.Name,
                            Area = Math.Round(area, 2),
                            Volume = Math.Round(volume, 2),
                            RawRoom = r
                        });
                    }
                }

                if (roomItems.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Найденные помещения имеют нулевую площадь. Проверьте границы помещений.");
                    return Result.Cancelled;
                }

                // 2. Сбор параметров помещений для записи результатов
                List<string> paramNames = GetWritableRoomDoubleParameters(doc);

                // 3. Открываем WPF окно
                HeatLossWindow window = new HeatLossWindow(roomItems, paramNames);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                // Обработчики событий
                window.CalculateRequested += () => PerformCalculation(window, doc);
                window.WriteToRevitRequested += () => WriteBackToRevit(window, doc);
                window.ExportExcelRequested += () => ExportToExcel(window, doc);

                // Выполняем начальный расчет с дефолтными коэффициентами
                PerformCalculation(window, doc);

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде расчета теплопотерь", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private static List<string> GetWritableRoomDoubleParameters(Document doc)
        {
            var pNames = new List<string>();
            var room = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .FirstOrDefault();
            if (room != null)
            {
                foreach (Parameter p in room.Parameters)
                {
                    if (p.StorageType == StorageType.Double && !p.IsReadOnly)
                    {
                        pNames.Add(p.Definition.Name);
                    }
                }
            }
            if (!pNames.Contains("Теплопотери")) pNames.Insert(0, "Теплопотери");
            return pNames.Distinct().ToList();
        }

        private void PerformCalculation(HeatLossWindow window, Document doc)
        {
            double tempIndoor = window.TempIndoor;
            double tempOutdoor = window.TempOutdoor;
            double dT = tempIndoor - tempOutdoor;

            double uWallExt = window.UWallExt;
            double uWindow = window.UWindow;
            double uDoor = window.UDoor;
            double uRoof = window.URoof;
            double uFloor = window.UFloor;
            double airChanges = window.AirChanges;

            SpatialElementBoundaryOptions boundaryOptions = new SpatialElementBoundaryOptions();

            foreach (var item in window.Rooms)
            {
                SpatialElement room = item.RawRoom as SpatialElement;
                if (room == null) continue;

                double roomAreaSqm = item.Area;
                double roomVolumeCum = item.Volume;

                double qTransmission = 0;

                // Получение границ помещения
                var boundarySegments = room.GetBoundarySegments(boundaryOptions);
                if (boundarySegments != null)
                {
                    foreach (var loop in boundarySegments)
                    {
                        foreach (var segment in loop)
                        {
                            double segLengthM = segment.GetCurve().Length * 0.3048;
                            
                            // Оценка высоты помещения
                            double roomHeightM = 3.0; // по умолчанию 3м
                            try
                            {
                                double heightParamVal = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT)?.AsDouble() ?? 0;
                                if (heightParamVal > 0) roomHeightM = heightParamVal * 0.3048;
                            }
                            catch { }

                            double wallAreaSqm = segLengthM * roomHeightM;

                            // Разрешение совместимости версий Revit для segment.ElementId / segment.GetElementId() через рефлексию
                            ElementId boundaryId = ElementId.InvalidElementId;
                            try
                            {
                                var prop = segment.GetType().GetProperty("ElementId");
                                if (prop != null)
                                {
                                    boundaryId = prop.GetValue(segment) as ElementId;
                                }
                                else
                                {
                                    var method = segment.GetType().GetMethod("GetElementId");
                                    if (method != null)
                                    {
                                        boundaryId = method.Invoke(segment, null) as ElementId;
                                    }
                                }
                            }
                            catch { }

                            Element boundaryElement = null;
                            if (boundaryId != null && boundaryId != ElementId.InvalidElementId)
                            {
                                boundaryElement = doc.GetElement(boundaryId);
                            }

                            double uValue = uWallExt;

                            if (boundaryElement != null)
                            {
                                string catName = boundaryElement.Category?.Name?.ToLower() ?? "";
                                if (catName.Contains("окно") || catName.Contains("window"))
                                {
                                    uValue = uWindow;
                                }
                                else if (catName.Contains("двер") || catName.Contains("door"))
                                {
                                    uValue = uDoor;
                                }
                                else if (catName.Contains("перекрыт") || catName.Contains("roof") || catName.Contains("крыш"))
                                {
                                    uValue = uRoof;
                                }
                                else if (catName.Contains("пол") || catName.Contains("floor"))
                                {
                                    uValue = uFloor;
                                }
                                else if (boundaryElement is Wall wall)
                                {
                                    var wallType = wall.WallType;
                                    var functionParam = wallType.get_Parameter(BuiltInParameter.FUNCTION_PARAM);
                                    if (functionParam != null && functionParam.HasValue)
                                    {
                                        if (functionParam.AsInteger() == 1) // 1 = Interior
                                        {
                                            uValue = 0.0; // Внутренние стены не учитываем в расчете теплопотерь наружу
                                        }
                                    }
                                }
                            }

                            qTransmission += uValue * wallAreaSqm * dT;
                        }
                    }
                }

                // Запасной расчет по площади
                if (qTransmission == 0)
                {
                    qTransmission = roomAreaSqm * 45.0; // Усредненно 45 Вт на кв.м
                }

                // Расчет вентиляции: Q = 0.337 * Объем * Кратность * dT
                double qVentilation = 0.337 * roomVolumeCum * airChanges * dT;

                item.TransmissionLoss = Math.Round(qTransmission, 1);
                item.VentilationLoss = Math.Round(qVentilation, 1);
                item.TotalLoss = Math.Round(qTransmission + qVentilation, 1);
            }
        }

        private void WriteBackToRevit(HeatLossWindow window, Document doc)
        {
            string paramName = window.SelectedParameter;
            if (string.IsNullOrEmpty(paramName))
            {
                TaskDialog.Show("Ошибка", "Не выбран параметр для записи.");
                return;
            }

            var firstRoom = window.Rooms.FirstOrDefault()?.RawRoom as SpatialElement;
            if (firstRoom == null) return;

            Parameter checkP = firstRoom.LookupParameter(paramName);
            if (checkP == null)
            {
                TaskDialog.Show("Внимание", $"Параметр с именем '{paramName}' не найден у помещений. Запись невозможна. Убедитесь, что параметр добавлен к категории Помещения.");
                return;
            }
            if (checkP.IsReadOnly)
            {
                TaskDialog.Show("Ошибка", $"Параметр '{paramName}' доступен только для чтения.");
                return;
            }

            using (Transaction tx = new Transaction(doc, "Запись теплопотерь BimboClub"))
            {
                tx.Start();
                int count = 0;
                foreach (var item in window.Rooms)
                {
                    SpatialElement room = item.RawRoom as SpatialElement;
                    if (room == null) continue;

                    Parameter p = room.LookupParameter(paramName);
                    if (p != null)
                    {
                        p.Set(item.TotalLoss);
                        count++;
                    }
                }
                tx.Commit();
                TaskDialog.Show("Успех", $"Теплопотери успешно записаны в параметр '{paramName}' для {count} помещений.");
            }
        }

        private void ExportToExcel(HeatLossWindow window, Document doc)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Экспорт теплопотерь в Excel - BimboClub Tools",
                Filter = "Excel Workbook (*.xml)|*.xml|Excel 97-2003 (*.xls)|*.xls",
                FileName = $"{string.Concat(doc.Title.Split(Path.GetInvalidFileNameChars())).Trim()}_Теплопотери.xls",
                DefaultExt = "xls",
                AddExtension = true
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                var table = new ExcelTable { SheetName = "Расчет теплопотерь" };
                table.Headers = new List<string> { "Номер помещения", "Имя помещения", "Площадь (м²)", "Объем (м³)", "Трансмиссия (Вт)", "Вентиляция (Вт)", "Теплопотери всего (Вт)" };
                table.NumericSumColumns.Add(2);
                table.NumericSumColumns.Add(3);
                table.NumericSumColumns.Add(4);
                table.NumericSumColumns.Add(5);
                table.NumericSumColumns.Add(6);

                foreach (var item in window.Rooms)
                {
                    table.Rows.Add(new List<object>
                    {
                        item.Number,
                        item.Name,
                        item.Area,
                        item.Volume,
                        item.TransmissionLoss,
                        item.VentilationLoss,
                        item.TotalLoss
                    });
                }

                ExcelWriter.Write(dialog.FileName, new List<ExcelTable> { table });
                TaskDialog.Show("Успех", $"Данные расчета теплопотерь успешно экспортированы в:\n{dialog.FileName}");
            }
        }
    }
}
