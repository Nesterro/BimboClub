using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class ExcelExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "Не найден активный документ Revit.";
                return Result.Failed;
            }

            try
            {
                Logger.Log("Запуск команды экспорта ведомостей и спецификаций в Excel.");

                // 1. Сбор всех спецификаций проекта
                var schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(vs => !vs.Definition.IsKeySchedule)
                    .OrderBy(vs => vs.Name)
                    .ToList();

                if (schedules.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В проекте не найдено ведомостей и спецификаций для экспорта.");
                    return Result.Cancelled;
                }

                // Подготовка элементов для UI
                var uiItems = schedules.Select(vs => new ScheduleExportItem
                {
                    ScheduleName = vs.Name,
                    RawSchedule = vs
                }).ToList();

                // 2. Открытие WPF окна настроек экспорта
                var window = new ExcelExportWindow(uiItems);
                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                // Загружаем сохраненные настройки
                var settings = ExcelExportSettings.Load();

                // 3. Выбор спецификаций для экспорта
                List<ViewSchedule> selectedSchedules = new List<ViewSchedule>();
                if (window.ExportCurrentOnly)
                {
                    ViewSchedule activeSchedule = doc.ActiveView as ViewSchedule;
                    if (activeSchedule != null && !activeSchedule.Definition.IsKeySchedule)
                    {
                        selectedSchedules.Add(activeSchedule);
                    }
                    else if (window.SelectedSchedules.Count > 0)
                    {
                        selectedSchedules.Add(window.SelectedSchedules.First() as ViewSchedule);
                    }
                    else
                    {
                        TaskDialog.Show("Внимание", "Активный вид не является спецификацией, и в списке не выбрано ни одной спецификации.");
                        return Result.Cancelled;
                    }
                }
                else
                {
                    selectedSchedules = window.SelectedSchedules.Cast<ViewSchedule>().ToList();
                }

                if (selectedSchedules.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Не выбрано ни одной спецификации для экспорта.");
                    return Result.Cancelled;
                }

                // 4. Папка для экспорта
                string exportFolder = settings.ExportFolder;
                if (string.IsNullOrEmpty(exportFolder) || !Directory.Exists(exportFolder))
                {
                    exportFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                // 5. Временная папка для экспорта TSV из Revit
                string tempDir = Path.Combine(Path.GetTempPath(), "BimboClub_Export_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Опции экспорта Revit
                var revitExportOpts = new ViewScheduleExportOptions
                {
                    FieldDelimiter = "\t",
                    TextQualifier = ExportTextQualifier.DoubleQuote,
                    ColumnHeaders = ExportColumnHeaders.OneRow,
                    Title = settings.ExportHeader
                };

                List<ExcelTable> parsedTables = new List<ExcelTable>();

                // Экспортируем каждую спецификацию в TSV и парсим её
                foreach (var vs in selectedSchedules)
                {
                    string safeName = string.Concat(vs.Name.Split(Path.GetInvalidFileNameChars())).Trim();
                    string tempFileName = $"{safeName}_{Guid.NewGuid()}.txt";
                    
                    try
                    {
                        vs.Export(tempDir, tempFileName, revitExportOpts);
                        string tempFilePath = Path.Combine(tempDir, tempFileName);

                        if (File.Exists(tempFilePath))
                        {
                            // Очистка имени вкладки
                            string sheetName = vs.Name;
                            char[] invalidSheetChars = new char[] { ':', '\\', '/', '?', '*', '[', ']' };
                            foreach (char c in invalidSheetChars)
                            {
                                sheetName = sheetName.Replace(c.ToString(), "");
                            }
                            if (sheetName.Length > 30)
                            {
                                sheetName = sheetName.Substring(0, 30);
                            }

                            ExcelTable table = ParseTsvToExcelTable(tempFilePath, sheetName, settings.PreserveFormatting);
                            parsedTables.Add(table);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Не удалось экспортировать спецификацию '{vs.Name}'", ex);
                    }
                }

                // Очистка временной папки
                try { Directory.Delete(tempDir, true); } catch { }

                if (parsedTables.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Не удалось экспортировать ни одну спецификацию.");
                    return Result.Failed;
                }

                // 6. Запись в Excel
                string prefix = settings.FilePrefix;
                if (!string.IsNullOrEmpty(prefix))
                {
                    prefix = string.Concat(prefix.Split(Path.GetInvalidFileNameChars())).Trim() + "_";
                }

                if (settings.EachScheduleInSeparateFile)
                {
                    // Каждую спецификацию в отдельный файл
                    foreach (var table in parsedTables)
                    {
                        string safeFileName = string.Concat(table.SheetName.Split(Path.GetInvalidFileNameChars())).Trim();
                        string filePath = Path.Combine(exportFolder, $"{prefix}{safeFileName}.xls");
                        ExcelWriter.Write(filePath, new List<ExcelTable> { table });
                    }
                    TaskDialog.Show("Успех", $"Экспорт завершен!\nУспешно экспортировано файлов: {parsedTables.Count}\nПапка сохранения: {exportFolder}");
                }
                else
                {
                    // Все в один файл на отдельные листы
                    string singleFileName = settings.FileName;
                    if (string.IsNullOrEmpty(singleFileName))
                    {
                        singleFileName = "Спецификации";
                    }
                    singleFileName = string.Concat(singleFileName.Split(Path.GetInvalidFileNameChars())).Trim();
                    string filePath = Path.Combine(exportFolder, $"{prefix}{singleFileName}.xls");

                    ExcelWriter.Write(filePath, parsedTables);
                    TaskDialog.Show("Успех", $"Экспорт завершен!\nУспешно экспортировано листов: {parsedTables.Count}\nФайл сохранен: {filePath}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка при экспорте спецификаций в Excel", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private static ExcelTable ParseTsvToExcelTable(string filePath, string sheetName, bool preserveFormatting)
        {
            var table = new ExcelTable { SheetName = sheetName };
            if (!File.Exists(filePath)) return table;

            // Спецификации Revit экспортируются в UTF-16 LE (Unicode)
            string[] lines = File.ReadAllLines(filePath, Encoding.Unicode);
            if (lines.Length == 0) return table;

            int lineIndex = 0;

            // Проверка наличия заголовка (первая строка - название, вторая - пустая)
            bool hasTitle = false;
            string titleText = "";
            if (lines.Length > 2 && string.IsNullOrEmpty(lines[1].Trim()))
            {
                titleText = lines[0].Trim().Trim('"');
                hasTitle = true;
                lineIndex = 2; // Пропускаем заголовок и пустую строку
            }

            if (lineIndex < lines.Length)
            {
                // Чтение колонок заголовка (Header)
                string headerLine = lines[lineIndex++];
                table.Headers = ParseTsvLine(headerLine);
            }

            // Чтение данных строк
            while (lineIndex < lines.Length)
            {
                string dataLine = lines[lineIndex++];
                if (string.IsNullOrEmpty(dataLine.Trim())) continue;

                var rowData = ParseTsvLine(dataLine);
                var convertedRow = new List<object>();

                foreach (var cellStr in rowData)
                {
                    // Пытаемся конвертировать строки в числа для сохранения числового формата ячейки
                    if (double.TryParse(cellStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
                    {
                        convertedRow.Add(dVal);
                    }
                    else if (double.TryParse(cellStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal2))
                    {
                        convertedRow.Add(dVal2);
                    }
                    else
                    {
                        convertedRow.Add(cellStr);
                    }
                }
                table.Rows.Add(convertedRow);
            }

            if (hasTitle && !string.IsNullOrEmpty(titleText))
            {
                table.Title = titleText;
            }

            return table;
        }

        private static List<string> ParseTsvLine(string line)
        {
            var result = new List<string>();
            string[] parts = line.Split('\t');
            foreach (var part in parts)
            {
                result.Add(part.Trim().Trim('"'));
            }
            return result;
        }
    }
}
