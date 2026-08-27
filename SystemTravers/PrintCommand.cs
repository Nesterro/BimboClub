using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class PrintCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Запуск команды Печать чертежей (Print Master).");

                // 1. Собираем все листы в модели
                FilteredElementCollector sheetCollector = new FilteredElementCollector(doc);
                List<ViewSheet> rawSheets = sheetCollector
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder) // Исключаем листы-заглушки
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                // Собираем все виды
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc);
                List<View> rawViews = viewCollector
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate 
                             && v.CanBePrinted 
                             && !(v is ViewSheet)
                             && v.ViewType != ViewType.Schedule
                             && v.ViewType != ViewType.ColumnSchedule
                             && v.ViewType != ViewType.PanelSchedule
                             && v.ViewType != ViewType.Internal
                             && v.ViewType != ViewType.ProjectBrowser
                             && v.ViewType != ViewType.SystemBrowser)
                    .OrderBy(v => v.Name)
                    .ToList();

                if (rawSheets.Count == 0 && rawViews.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В текущем проекте не найдено оформленных листов чертежей или видов для печати.");
                    return Result.Cancelled;
                }

                // 2. Анализируем форматы рамок для каждого листа
                List<SheetItem> sheetItems = new List<SheetItem>();
                foreach (ViewSheet sheet in rawSheets)
                {
                    string paperFormat = "Не определен";
                    string orientation = "Альбомная";

                    // Ищем рамки основной надписи на листе
                    FilteredElementCollector titleBlocks = new FilteredElementCollector(doc, sheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .OfClass(typeof(FamilyInstance));

                    double widthMm = 0;
                    double heightMm = 0;

                    foreach (FamilyInstance tb in titleBlocks)
                    {
                        Parameter wParam = tb.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                        Parameter hParam = tb.get_Parameter(BuiltInParameter.SHEET_HEIGHT);
                        if (wParam != null && hParam != null)
                        {
                            // Конвертируем футы в миллиметры
                            widthMm = wParam.AsDouble() * 304.8;
                            heightMm = hParam.AsDouble() * 304.8;
                            break; // Берем первую рамку
                        }
                    }

                    if (widthMm > 0 && heightMm > 0)
                    {
                        double max = Math.Max(widthMm, heightMm);
                        double min = Math.Min(widthMm, heightMm);
                        orientation = (widthMm > heightMm) ? "Альбомная" : "Книжная";

                        // Определение формата по ГОСТ (с допуском ±10мм)
                        if (Math.Abs(min - 210) < 10 && Math.Abs(max - 297) < 10) paperFormat = "A4";
                        else if (Math.Abs(min - 297) < 10 && Math.Abs(max - 420) < 10) paperFormat = "A3";
                        else if (Math.Abs(min - 420) < 10 && Math.Abs(max - 594) < 10) paperFormat = "A2";
                        else if (Math.Abs(min - 594) < 10 && Math.Abs(max - 841) < 10) paperFormat = "A1";
                        else if (Math.Abs(min - 841) < 10 && Math.Abs(max - 1189) < 10) paperFormat = "A0";
                        // Кратные форматы ГОСТ
                        else if (Math.Abs(min - 297) < 10 && Math.Abs(max - 630) < 10) paperFormat = "A4x3";
                        else if (Math.Abs(min - 297) < 10 && Math.Abs(max - 841) < 10) paperFormat = "A4x4";
                        else if (Math.Abs(min - 297) < 10 && Math.Abs(max - 1051) < 10) paperFormat = "A4x5";
                        else if (Math.Abs(min - 420) < 10 && Math.Abs(max - 841) < 10) paperFormat = "A3x3";
                        else if (Math.Abs(min - 420) < 10 && Math.Abs(max - 1189) < 10) paperFormat = "A3x4";
                        else if (Math.Abs(min - 420) < 10 && Math.Abs(max - 1261) < 12) paperFormat = "A3x5";
                        else paperFormat = $"Пользовательский ({Math.Round(widthMm)}x{Math.Round(heightMm)})";
                    }

                    sheetItems.Add(new SheetItem
                    {
                        SheetNumber = sheet.SheetNumber,
                        SheetName = sheet.Name,
                        PaperFormat = paperFormat,
                        Orientation = orientation,
                        RawSheetObject = sheet,
                        IsView = false,
                        UniqueId = sheet.UniqueId
                    });
                }

                // Добавляем виды в список
                foreach (View view in rawViews)
                {
                    sheetItems.Add(new SheetItem
                    {
                        SheetNumber = "[Вид]",
                        SheetName = view.Name,
                        PaperFormat = "Не определен",
                        Orientation = "Альбомная",
                        RawSheetObject = view,
                        IsView = true,
                        UniqueId = view.UniqueId
                    });
                }

                // 3. Получаем шифр проекта и дефолтный раздел
                string projNum = doc.ProjectInformation.Number;
                if (string.IsNullOrEmpty(projNum)) projNum = "2026-05";

                string defaultSection = "ОВ";
                if (rawSheets.Count > 0)
                {
                    defaultSection = GetSheetSection(rawSheets.First());
                }
                else if (rawViews.Count > 0)
                {
                    defaultSection = GetSheetSection(rawViews.First());
                }

                // Ищем все сохраненные наборы листов (ViewSheetSet)
                FilteredElementCollector setCollector = new FilteredElementCollector(doc);
                List<ViewSheetSet> sheetSets = setCollector
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .OrderBy(s => s.Name)
                    .ToList();

                Dictionary<string, List<string>> sheetSetMap = new Dictionary<string, List<string>>();
                foreach (ViewSheetSet set in sheetSets)
                {
                    List<string> ids = new List<string>();
                    foreach (View view in set.Views)
                    {
                        ids.Add(view.UniqueId);
                    }
                    sheetSetMap[set.Name] = ids;
                }

                string activeViewId = doc.ActiveView?.UniqueId ?? "";

                // 3.5 Получаем список настроек DWG экспорта из модели
                List<string> dwgSetupNames = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportDWGSettings))
                    .Cast<ExportDWGSettings>()
                    .Select(s => s.Name)
                    .OrderBy(n => n)
                    .ToList();

                // 4. Открываем интерфейс WPF
                PrintWindow window = new PrintWindow(sheetItems, projNum, defaultSection, sheetSetMap, activeViewId, dwgSetupNames);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true && window.IsExportRequested)
                {
                    List<SheetItem> toExport = window.SelectedSheets;
                    string folderPath = window.OutputFolderPath;
                    string namingPattern = window.NamingPattern;
                    bool combine = window.CombineToSinglePdf;

                    // Проверяем версию Revit на предмет поддержки нативного PDF экспорта (2022+)
                    int revitVer = 2024;
                    int.TryParse(doc.Application.VersionNumber, out revitVer);

                    if (window.ExportPdf && revitVer < 2022)
                    {
                        TaskDialog.Show("Внимание", "Нативный экспорт в PDF через Revit API поддерживается начиная с версии Revit 2022.\nПожалуйста, используйте Revit 2022-2026 для работы этого модуля.");
                        return Result.Failed;
                    }

                    int exportedPdfs = 0;
                    int exportedDwgs = 0;

                    // 1. PDF Export (Combined or Single)
                    if (window.ExportPdf)
                    {
                        if (combine)
                        {
                            try
                            {
                                string firstSection = toExport.Count > 0 ? GetSheetSection(toExport.First().RawSheetObject as View) : defaultSection;
                                string fileName = namingPattern
                                    .Replace("{ProjectNumber}", projNum)
                                    .Replace("{Section}", firstSection)
                                    .Replace("{SheetNumber}", "Комплект")
                                    .Replace("{SheetName}", "Чертежей");

                                fileName = MakeValidFileName(fileName) + ".pdf";

                                List<View> viewsToExport = toExport.Select(item => item.RawSheetObject as View).Where(v => v != null).ToList();
                                NativePdfExporter.ExportPDFCombined(doc, viewsToExport, folderPath, fileName, window.FixQuotes);
                                exportedPdfs = viewsToExport.Count;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Ошибка при объединении в один PDF", ex);
                                TaskDialog.Show("Ошибка", "Не удалось объединить элементы в один PDF: " + ex.Message);
                            }
                        }
                        else
                        {
                            foreach (SheetItem item in toExport)
                            {
                                View view = item.RawSheetObject as View;
                                if (view == null) continue;

                                try
                                {
                                    string sheetSection = GetSheetSection(view);
                                    string sheetNum = view is ViewSheet vs ? vs.SheetNumber : "";
                                    string fileName = namingPattern
                                        .Replace("{ProjectNumber}", projNum)
                                        .Replace("{Section}", sheetSection)
                                        .Replace("{SheetNumber}", sheetNum)
                                        .Replace("{SheetName}", view.Name);

                                    fileName = fileName.Replace("__", "_").Replace("--", "-");
                                    if (fileName.StartsWith("_") || fileName.StartsWith("-")) fileName = fileName.Substring(1);
                                    if (fileName.EndsWith("_") || fileName.EndsWith("-")) fileName = fileName.Substring(0, fileName.Length - 1);

                                    fileName = MakeValidFileName(fileName) + ".pdf";

                                    NativePdfExporter.ExportPDF(doc, view, folderPath, fileName, window.FixQuotes);
                                    exportedPdfs++;
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Не удалось экспортировать PDF для {view.Name}", ex);
                                }
                            }
                        }
                    }

                    // 2. DWG Export
                    if (window.ExportDwg)
                    {
                        string selectedDwgSetup = window.SelectedDwgSetup;
                        foreach (SheetItem item in toExport)
                        {
                            View view = item.RawSheetObject as View;
                            if (view == null) continue;

                            try
                            {
                                string sheetSection = GetSheetSection(view);
                                string sheetNum = view is ViewSheet vs ? vs.SheetNumber : "";
                                string fileName = namingPattern
                                    .Replace("{ProjectNumber}", projNum)
                                    .Replace("{Section}", sheetSection)
                                    .Replace("{SheetNumber}", sheetNum)
                                    .Replace("{SheetName}", view.Name);

                                fileName = fileName.Replace("__", "_").Replace("--", "-");
                                if (fileName.StartsWith("_") || fileName.StartsWith("-")) fileName = fileName.Substring(1);
                                if (fileName.EndsWith("_") || fileName.EndsWith("-")) fileName = fileName.Substring(0, fileName.Length - 1);

                                fileName = MakeValidFileName(fileName);

                                NativePdfExporter.ExportDWG(doc, view, folderPath, fileName, selectedDwgSetup);
                                exportedDwgs++;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Не удалось экспортировать DWG для {view.Name}", ex);
                            }
                        }
                    }

                    // Summary
                    List<string> resultParts = new List<string>();
                    if (window.ExportPdf) resultParts.Add($"PDF файлов: {exportedPdfs}");
                    if (window.ExportDwg) resultParts.Add($"DWG файлов: {exportedDwgs}");

                    string statusMsg = string.Join("\n", resultParts);
                    
                    ExportResultWindow resultWindow = new ExportResultWindow(statusMsg, folderPath);
                    var resHelper = new System.Windows.Interop.WindowInteropHelper(resultWindow);
                    resHelper.Owner = uiapp.MainWindowHandle;

                    resultWindow.ShowDialog();
                    if (resultWindow.OpenFolderRequested)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = folderPath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Не удалось открыть папку после экспорта", ex);
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения PrintCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private string GetSheetSection(View sheet)
        {
            foreach (Parameter param in sheet.Parameters)
            {
                if (param.Definition != null)
                {
                    string name = param.Definition.Name;
                    if (name.Contains("Раздел") || name.Contains("раздел") || name.Contains("Штамп"))
                    {
                        if (param.StorageType == StorageType.String)
                        {
                            string val = param.AsString();
                            if (!string.IsNullOrEmpty(val)) return val;
                        }
                    }
                }
            }
            return "ОВ";
        }

        private string MakeValidFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }

    /// <summary>
    /// Изолированный класс для вызова нативного PDF/DWG/IFC экспорта
    /// </summary>
    public static class NativePdfExporter
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ExportPDF(Document doc, View view, string folderPath, string targetFileName, bool fixQuotes = true)
        {
            PDFExportOptions options = new PDFExportOptions();
            options.Combine = false;
            options.PaperFormat = ExportPaperFormat.Default;

            var beforeFiles = Directory.GetFiles(folderPath, "*.pdf");

            IList<ElementId> viewIds = new List<ElementId> { view.Id };
            using (Transaction trans = new Transaction(doc, "BimboClub PDF Export"))
            {
                trans.Start();
                if (fixQuotes)
                {
                    QuoteSanitizer.SanitizeDocument(doc, viewIds, out _, out _);
                    doc.Regenerate();
                }

                doc.Export(folderPath, viewIds, options);

                if (fixQuotes)
                {
                    trans.RollBack();
                }
                else
                {
                    trans.Commit();
                }
            }

            var afterFiles = Directory.GetFiles(folderPath, "*.pdf");
            var newFiles = afterFiles.Except(beforeFiles).ToList();

            if (newFiles.Count > 0)
            {
                string tempPdfPath = newFiles.First();
                string targetPdfPath = Path.Combine(folderPath, targetFileName);

                if (File.Exists(targetPdfPath))
                {
                    File.Delete(targetPdfPath);
                }

                File.Move(tempPdfPath, targetPdfPath);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ExportPDFCombined(Document doc, List<View> sheets, string folderPath, string targetFileName, bool fixQuotes = true)
        {
            PDFExportOptions options = new PDFExportOptions();
            options.Combine = true;
            options.FileName = Path.GetFileNameWithoutExtension(targetFileName);
            options.PaperFormat = ExportPaperFormat.Default;

            IList<ElementId> viewIds = sheets.Select(s => s.Id).ToList();
            using (Transaction trans = new Transaction(doc, "BimboClub PDF Combined Export"))
            {
                trans.Start();
                if (fixQuotes)
                {
                    QuoteSanitizer.SanitizeDocument(doc, viewIds, out _, out _);
                    doc.Regenerate();
                }

                doc.Export(folderPath, viewIds, options);

                if (fixQuotes)
                {
                    trans.RollBack();
                }
                else
                {
                    trans.Commit();
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ExportDWG(Document doc, View view, string folderPath, string targetFileName, string setupName)
        {
            DWGExportOptions options = null;

            if (!string.IsNullOrEmpty(setupName) && setupName != "<По умолчанию>")
            {
                ExportDWGSettings setup = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportDWGSettings))
                    .Cast<ExportDWGSettings>()
                    .FirstOrDefault(s => s.Name == setupName);

                if (setup != null)
                {
                    options = setup.GetDWGExportOptions();
                }
            }

            if (options == null)
            {
                options = new DWGExportOptions();
                options.MergedViews = true;
            }

            var beforeFiles = Directory.GetFiles(folderPath, "*.dwg");

            IList<ElementId> viewIds = new List<ElementId> { view.Id };
            doc.Export(folderPath, targetFileName, viewIds, options);

            var afterFiles = Directory.GetFiles(folderPath, "*.dwg");
            var newFiles = afterFiles.Except(beforeFiles).ToList();
            if (newFiles.Count > 0)
            {
                string tempDwgPath = newFiles.First();
                string targetDwgPath = Path.Combine(folderPath, targetFileName + ".dwg");
                if (tempDwgPath != targetDwgPath)
                {
                    if (File.Exists(targetDwgPath))
                    {
                        try { File.Delete(targetDwgPath); } catch {}
                    }
                    File.Move(tempDwgPath, targetDwgPath);
                }
            }
        }
    }
}
