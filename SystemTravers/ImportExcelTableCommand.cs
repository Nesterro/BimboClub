using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace BimboClub
{
    public class ExcelCellData
    {
        public int RowIndex { get; set; }
        public int ColIndex { get; set; }
        public string Value { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public double FontSize { get; set; }
        public string Alignment { get; set; } // "Left", "Center", "Right"
    }

    public class ExcelTableData
    {
        public List<ExcelCellData> Cells { get; set; } = new List<ExcelCellData>();
        public Dictionary<int, double> ColWidthsMm { get; set; } = new Dictionary<int, double>();
        public Dictionary<int, double> RowHeightsMm { get; set; } = new Dictionary<int, double>();
    }

    public class ExcelLinkData
    {
        public string ExcelPath { get; set; }
        public string SheetName { get; set; }
        public string CellRange { get; set; }
        public List<int> ElementIds { get; set; }
        public string LastImported { get; set; }
    }

    public static class ExcelLinkStorage
    {
        private static readonly Guid SchemaGuid = new Guid("D3228BE6-5CBE-4A63-8F74-D72D9D862C18");

        public static Autodesk.Revit.DB.ExtensibleStorage.Schema GetOrCreateSchema()
        {
            Autodesk.Revit.DB.ExtensibleStorage.Schema schema = Autodesk.Revit.DB.ExtensibleStorage.Schema.Lookup(SchemaGuid);
            if (schema != null) return schema;

            Autodesk.Revit.DB.ExtensibleStorage.SchemaBuilder builder = new Autodesk.Revit.DB.ExtensibleStorage.SchemaBuilder(SchemaGuid);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.SetSchemaName("ExcelTableLink");

            builder.AddSimpleField("ExcelPath", typeof(string));
            builder.AddSimpleField("SheetName", typeof(string));
            builder.AddSimpleField("CellRange", typeof(string));
            builder.AddArrayField("ElementIds", typeof(int));
            builder.AddSimpleField("LastImported", typeof(string));

            return builder.Finish();
        }

        public static void SaveLink(View view, string excelPath, string sheetName, string cellRange, List<int> elementIds)
        {
            Autodesk.Revit.DB.ExtensibleStorage.Schema schema = GetOrCreateSchema();
            Entity entity = new Entity(schema);
            entity.Set("ExcelPath", excelPath);
            entity.Set("SheetName", sheetName);
            entity.Set("CellRange", cellRange);
            entity.Set("ElementIds", (IList<int>)elementIds);
            entity.Set("LastImported", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            view.SetEntity(entity);
        }

        public static ExcelLinkData LoadLink(View view)
        {
            Autodesk.Revit.DB.ExtensibleStorage.Schema schema = GetOrCreateSchema();
            Entity entity = view.GetEntity(schema);
            if (entity == null || !entity.IsValid()) return null;

            try
            {
                IList<int> ids = entity.Get<IList<int>>("ElementIds");
                return new ExcelLinkData
                {
                    ExcelPath = entity.Get<string>("ExcelPath"),
                    SheetName = entity.Get<string>("SheetName"),
                    CellRange = entity.Get<string>("CellRange"),
                    ElementIds = ids != null ? ids.ToList() : new List<int>(),
                    LastImported = entity.Get<string>("LastImported")
                };
            }
            catch
            {
                return null;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ImportExcelTableCommand : IExternalCommand
    {
        private const double MmToFeetFactor = 0.00328084;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Gather all existing drafting views and legends
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc);
                List<View> allDraftingAndLegends = viewCollector
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.ViewType == ViewType.DraftingView || v.ViewType == ViewType.Legend)
                    .ToList();

                List<ViewLinkItem> links = new List<ViewLinkItem>();
                List<object> availableViews = new List<object>();

                foreach (View v in allDraftingAndLegends)
                {
                    if (v.IsTemplate) continue;

                    availableViews.Add(new Tuple<string, object>($"{(v.ViewType == ViewType.Legend ? "[Легенда]" : "[Чертеж]")} {v.Name}", v));

                    ExcelLinkData link = ExcelLinkStorage.LoadLink(v);
                    if (link != null)
                    {
                        links.Add(new ViewLinkItem
                        {
                            ViewName = v.Name,
                            ExcelPath = link.ExcelPath,
                            SheetName = link.SheetName,
                            CellRange = link.CellRange,
                            LastImported = link.LastImported,
                            RawView = v
                        });
                    }
                }

                // 2. Open dialog passing file sheets and print area query delegates
                ExcelImportWindow window = new ExcelImportWindow(
                    links, 
                    availableViews, 
                    (filePath) => GetWorksheets(filePath),
                    (filePath, sheetName) => GetSheetPrintArea(filePath, sheetName)
                );

                if (window.ShowDialog() == true)
                {
                    if (window.IsImportRequested)
                    {
                        using (Transaction t = new Transaction(doc, "Импорт таблицы Excel"))
                        {
                            t.Start();
                            ImportExcelToView(doc, window.ExcelPath, window.SheetName, window.CellRange, window.SelectedTargetView, window.NewViewName, window.CreateNewDrafting, window.CreateNewLegend);
                            t.Commit();
                        }
                        TaskDialog.Show("Успех", "Таблица успешно импортирована!");
                    }
                    else if (window.IsUpdateRequested)
                    {
                        int updatedCount = 0;
                        int failedCount = 0;
                        StringBuilder sb = new StringBuilder();

                        using (TransactionGroup tg = new TransactionGroup(doc, "Обновление таблиц Excel"))
                        {
                            tg.Start();

                            foreach (var link in window.SelectedExistingLinks)
                            {
                                View view = link.RawView as View;
                                if (view == null) continue;

                                try
                                {
                                    using (Transaction t = new Transaction(doc, $"Обновление вида {view.Name}"))
                                    {
                                        t.Start();
                                        ImportExcelToView(doc, link.ExcelPath, link.SheetName, link.CellRange, view, "", false, false);
                                        t.Commit();
                                    }
                                    updatedCount++;
                                }
                                catch (Exception ex)
                                {
                                    failedCount++;
                                    sb.AppendLine($"- {view.Name}: {ex.Message}");
                                }
                            }

                            tg.Assimilate();
                        }

                        if (failedCount == 0)
                        {
                            TaskDialog.Show("Успех", $"Успешно обновлено видов: {updatedCount}");
                        }
                        else
                        {
                            TaskDialog.Show("Результат обновления", $"Обновлено видов: {updatedCount}\nНе удалось обновить: {failedCount}\n\nОшибки:\n{sb.ToString()}");
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private static List<string> GetWorksheets(string filePath)
        {
            List<string> sheetNames = new List<string>();
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(filePath, false))
            {
                WorkbookPart wbPart = document.WorkbookPart;
                if (wbPart != null)
                {
                    foreach (Sheet sheet in wbPart.Workbook.Sheets)
                    {
                        if (sheet.Name != null)
                        {
                            sheetNames.Add(sheet.Name.Value);
                        }
                    }
                }
            }
            return sheetNames;
        }

        private static string GetSheetPrintArea(string filePath, string sheetName)
        {
            try
            {
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(filePath, false))
                {
                    WorkbookPart wbPart = document.WorkbookPart;
                    if (wbPart?.Workbook == null) return null;

                    int sheetIndex = -1;
                    int idx = 0;
                    foreach (Sheet s in wbPart.Workbook.Sheets)
                    {
                        if (s.Name != null && s.Name.Value.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                        {
                            sheetIndex = idx;
                            break;
                        }
                        idx++;
                    }

                    if (sheetIndex == -1) return null;

                    DefinedNames definedNames = wbPart.Workbook.DefinedNames;
                    if (definedNames != null)
                    {
                        foreach (DefinedName dn in definedNames.Elements<DefinedName>())
                        {
                            if (dn.Name != null && dn.Name.Value == "_xlnm.Print_Area")
                            {
                                bool isMatch = false;
                                if (dn.LocalSheetId != null && dn.LocalSheetId.Value == sheetIndex)
                                {
                                    isMatch = true;
                                }
                                else
                                {
                                    string text = dn.Text;
                                    if (!string.IsNullOrEmpty(text) && text.Contains("!"))
                                    {
                                        string refSheetName = text.Split('!')[0].Trim('\'');
                                        if (refSheetName.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            isMatch = true;
                                        }
                                    }
                                }

                                if (isMatch)
                                {
                                    string text = dn.Text;
                                    if (!string.IsNullOrEmpty(text) && text.Contains("!"))
                                    {
                                        string rangePart = text.Split('!')[1];
                                        return rangePart.Replace("$", "");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        private static ExcelTableData ReadExcelCells(string filePath, string sheetName, string cellRange)
        {
            ExcelTableData data = new ExcelTableData();

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(filePath, false))
            {
                WorkbookPart wbPart = document.WorkbookPart;
                Sheet sheet = wbPart.Workbook.Sheets.Cast<Sheet>().FirstOrDefault(s => s.Name == sheetName);
                if (sheet == null) throw new Exception($"Лист '{sheetName}' не найден в файле.");

                WorksheetPart wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id);
                SheetData sheetData = wsPart.Worksheet.Elements<SheetData>().First();
                List<Cell> cells = sheetData.Descendants<Cell>().ToList();

                var (minCol, minRow, maxCol, maxRow) = ParseRange(cellRange, cells);

                // Shared strings lookup
                SharedStringTablePart sstPart = wbPart.SharedStringTablePart;
                SharedStringTable sst = sstPart?.SharedStringTable;

                // Stylesheet lookup
                Stylesheet stylesheet = wbPart.WorkbookStylesPart?.Stylesheet;

                // 1. Column widths using Elements<Columns>().FirstOrDefault() to prevent ordering bugs
                Columns cols = wsPart.Worksheet.Elements<Columns>().FirstOrDefault();
                if (cols != null)
                {
                    foreach (Column col in cols.Elements<Column>())
                    {
                        int min = (int)col.Min.Value - 1;
                        int max = (int)col.Max.Value - 1;
                        double width = col.Width?.Value ?? 8.43;
                        for (int i = min; i <= max; i++)
                        {
                            // Map width (characters) to millimeters: ~2.2mm per char
                            data.ColWidthsMm[i] = width * 2.2;
                        }
                    }
                }

                // 2. Row heights and cells
                foreach (Row r in sheetData.Elements<Row>())
                {
                    int rowIndex = (int)r.RowIndex.Value - 1;
                    if (rowIndex < minRow || rowIndex > maxRow) continue;

                    double height = r.Height?.Value ?? 15.0;
                    // Map height (points) to millimeters: 1pt = 0.3527mm, with 20% vertical padding
                    data.RowHeightsMm[rowIndex] = height * 0.3527 * 1.2;

                    foreach (Cell c in r.Elements<Cell>())
                    {
                        var (colIndex, _) = ParseCellReference(c.CellReference.Value);
                        if (colIndex < minCol || colIndex > maxCol) continue;

                        string val = c.CellValue?.Text;
                        if (c.DataType != null && c.DataType.Value == CellValues.SharedString && sst != null)
                        {
                            int idx = int.Parse(val);
                            val = sst.ChildElements[idx].InnerText;
                        }
                        else if (val == null && c.InnerText != null)
                        {
                            val = c.InnerText;
                        }

                        if (string.IsNullOrEmpty(val)) continue;

                        // Lookup font and alignment formatting
                        bool bold = false;
                        bool italic = false;
                        double fontSize = 10.0;
                        string alignment = "Left";

                        if (c.StyleIndex != null && stylesheet?.CellFormats != null)
                        {
                            CellFormat format = stylesheet.CellFormats.ChildElements[(int)c.StyleIndex.Value] as CellFormat;
                            if (format != null)
                            {
                                if (format.FontId != null && stylesheet.Fonts != null)
                                {
                                    Font font = stylesheet.Fonts.ChildElements[(int)format.FontId.Value] as Font;
                                    if (font != null)
                                    {
                                        bold = font.Bold != null;
                                        italic = font.Italic != null;
                                        if (font.FontSize?.Val != null)
                                        {
                                            fontSize = font.FontSize.Val.Value;
                                        }
                                    }
                                }

                                if (format.Alignment?.Horizontal != null)
                                {
                                    var horiz = format.Alignment.Horizontal.Value;
                                    if (horiz == HorizontalAlignmentValues.Center) alignment = "Center";
                                    else if (horiz == HorizontalAlignmentValues.Right) alignment = "Right";
                                }
                            }
                        }

                        data.Cells.Add(new ExcelCellData
                        {
                            RowIndex = rowIndex,
                            ColIndex = colIndex,
                            Value = val,
                            IsBold = bold,
                            IsItalic = italic,
                            FontSize = fontSize,
                            Alignment = alignment
                        });
                    }
                }
            }

            return data;
        }

        private static (int colIndex, int rowIndex) ParseCellReference(string reference)
        {
            int colIndex = 0;
            int rowIndex = 0;
            int index = 0;
            while (index < reference.Length && char.IsLetter(reference[index]))
            {
                colIndex = colIndex * 26 + (reference[index] - 'A' + 1);
                index++;
            }
            colIndex--; // 0-based
            if (index < reference.Length)
            {
                int.TryParse(reference.Substring(index), out rowIndex);
                rowIndex--; // 0-based
            }
            return (colIndex, rowIndex);
        }

        private static (int minCol, int minRow, int maxCol, int maxRow) ParseRange(string rangeStr, List<Cell> cells)
        {
            if (string.IsNullOrWhiteSpace(rangeStr) || !rangeStr.Contains(":"))
            {
                if (cells.Count == 0) return (0, 0, 0, 0);
                int minCol = int.MaxValue;
                int minRow = int.MaxValue;
                int maxCol = 0;
                int maxRow = 0;
                foreach (var c in cells)
                {
                    var (col, row) = ParseCellReference(c.CellReference.Value);
                    if (col < minCol) minCol = col;
                    if (row < minRow) minRow = row;
                    if (col > maxCol) maxCol = col;
                    if (row > maxRow) maxRow = row;
                }
                return (minCol, minRow, maxCol, maxRow);
            }

            var parts = rangeStr.Split(':');
            var start = ParseCellReference(parts[0]);
            var end = ParseCellReference(parts[1]);
            return (start.colIndex, start.rowIndex, end.colIndex, end.rowIndex);
        }

        private static void ImportExcelToView(Document doc, string excelPath, string sheetName, string cellRange, object targetViewObj, string newViewName, bool createNewDrafting, bool createNewLegend)
        {
            // 1. Read the Excel table data
            ExcelTableData tableData = ReadExcelCells(excelPath, sheetName, cellRange);
            if (tableData.Cells.Count == 0) throw new Exception("Нет данных для импорта в указанном диапазоне.");

            // Determine table grid bounds
            int minCol = tableData.Cells.Min(c => c.ColIndex);
            int maxCol = tableData.Cells.Max(c => c.ColIndex);
            int minRow = tableData.Cells.Min(c => c.RowIndex);
            int maxRow = tableData.Cells.Max(c => c.RowIndex);

            // Fill missing column widths and row heights with realistic defaults
            for (int c = minCol; c <= maxCol; c++)
            {
                if (!tableData.ColWidthsMm.ContainsKey(c)) tableData.ColWidthsMm[c] = 30.0; // Default 30mm
            }
            for (int r = minRow; r <= maxRow; r++)
            {
                if (!tableData.RowHeightsMm.ContainsKey(r)) tableData.RowHeightsMm[r] = 6.0; // Default 6mm
            }

            // 2. Identify or create target View
            View targetView = null;
            if (createNewDrafting)
            {
                ViewFamilyType draftingFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(t => t.ViewFamily == ViewFamily.Drafting);

                if (draftingFamilyType == null) throw new Exception("Не найден тип семейства чертежного вида в проекте.");

                ViewDrafting newDrafting = ViewDrafting.Create(doc, draftingFamilyType.Id);
                newDrafting.Name = MakeUniqueViewName(doc, newViewName);
                newDrafting.Scale = 1; // 1:1 Scale
                targetView = newDrafting;
            }
            else if (createNewLegend)
            {
                View existingLegend = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.ViewType == ViewType.Legend && !v.IsTemplate);

                if (existingLegend == null)
                {
                    ViewFamilyType draftingFamilyType = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(t => t.ViewFamily == ViewFamily.Drafting);

                    ViewDrafting newDrafting = ViewDrafting.Create(doc, draftingFamilyType.Id);
                    newDrafting.Name = MakeUniqueViewName(doc, newViewName);
                    newDrafting.Scale = 1;
                    targetView = newDrafting;

                    TaskDialog.Show("Информация", "В проекте не найдено ни одной легенды для дублирования. Был автоматически создан чертежный вид.");
                }
                else
                {
                    ElementId duplicatedId = existingLegend.Duplicate(ViewDuplicateOption.Duplicate);
                    View newLegend = doc.GetElement(duplicatedId) as View;
                    newLegend.Name = MakeUniqueViewName(doc, newViewName);
                    newLegend.Scale = 1;

                    // Delete elements inside the duplicated legend view
                    var elemsToDelete = new FilteredElementCollector(doc, newLegend.Id)
                        .WhereElementIsNotElementType()
                        .Select(e => e.Id)
                        .ToList();
                    doc.Delete(elemsToDelete);

                    targetView = newLegend;
                }
            }
            else
            {
                targetView = targetViewObj as View;
                if (targetView == null) throw new Exception("Целевой вид не выбран.");

                // Clean up previous elements imported by this tool in the target view
                ExcelLinkData oldLink = ExcelLinkStorage.LoadLink(targetView);
                if (oldLink != null && oldLink.ElementIds != null)
                {
                    foreach (int id in oldLink.ElementIds)
                    {
                        ElementId elemId = new ElementId(id);
                        if (doc.GetElement(elemId) != null)
                        {
                            try { doc.Delete(elemId); } catch {}
                        }
                    }
                }
            }

            // 3. Compute Grid Coordinates scaled dynamically by View.Scale
            double scale = targetView != null ? targetView.Scale : 1.0;
            if (scale < 1.0) scale = 1.0;

            int numCols = maxCol - minCol + 1;
            int numRows = maxRow - minRow + 1;

            double[] colX = new double[numCols + 1];
            colX[0] = 0;
            for (int i = 0; i < numCols; i++)
            {
                int excelColIndex = minCol + i;
                colX[i + 1] = colX[i] + tableData.ColWidthsMm[excelColIndex] * MmToFeetFactor * scale;
            }

            double[] rowY = new double[numRows + 1];
            rowY[0] = 0;
            for (int i = 0; i < numRows; i++)
            {
                int excelRowIndex = minRow + i;
                rowY[i + 1] = rowY[i] - tableData.RowHeightsMm[excelRowIndex] * MmToFeetFactor * scale;
            }

            List<int> createdElementIds = new List<int>();

            // 4. Draw Grid Lines
            // Horizontal lines
            for (int r = 0; r <= numRows; r++)
            {
                XYZ p1 = new XYZ(colX[0], rowY[r], 0);
                XYZ p2 = new XYZ(colX[numCols], rowY[r], 0);
                DetailCurve curve = DrawLine(doc, targetView, p1, p2);
                if (curve != null) createdElementIds.Add(curve.Id.IntegerValue);
            }

            // Vertical lines
            for (int c = 0; c <= numCols; c++)
            {
                XYZ p1 = new XYZ(colX[c], rowY[0], 0);
                XYZ p2 = new XYZ(colX[c], rowY[numRows], 0);
                DetailCurve curve = DrawLine(doc, targetView, p1, p2);
                if (curve != null) createdElementIds.Add(curve.Id.IntegerValue);
            }

            // 5. Place TextNotes with wrap width and insertion offset scaled by view scale
            foreach (var cell in tableData.Cells)
            {
                int cIndex = cell.ColIndex - minCol;
                int rIndex = cell.RowIndex - minRow;

                double leftX = colX[cIndex];
                double rightX = colX[cIndex + 1];
                double topY = rowY[rIndex];
                double bottomY = rowY[rIndex + 1];

                double centerX = (leftX + rightX) / 2.0;
                double centerY = (topY + bottomY) / 2.0;

                HorizontalTextAlignment horiz = HorizontalTextAlignment.Center;
                Autodesk.Revit.DB.VerticalTextAlignment vert = Autodesk.Revit.DB.VerticalTextAlignment.Middle;
                XYZ insertionPoint = new XYZ(centerX, centerY, 0);

                if (cell.Alignment == "Left")
                {
                    horiz = HorizontalTextAlignment.Left;
                    double paddingFeet = 1.5 * MmToFeetFactor * scale; // 1.5mm offset
                    insertionPoint = new XYZ(leftX + paddingFeet, centerY, 0);
                }
                else if (cell.Alignment == "Right")
                {
                    horiz = HorizontalTextAlignment.Right;
                    double paddingFeet = 1.5 * MmToFeetFactor * scale; // 1.5mm offset
                    insertionPoint = new XYZ(rightX - paddingFeet, centerY, 0);
                }

                // Get or create text note type with unscaled paper height (millimeter-based)
                double textSize = cell.FontSize * 0.25; // Scale font size to Revit mm
                if (textSize < 1.5) textSize = 1.5;
                if (textSize > 10.0) textSize = 10.0;
                textSize = Math.Round(textSize * 2.0) / 2.0; // Round to nearest 0.5mm

                string typeName = $"Bimbo_Excel_{textSize}mm{(cell.IsBold ? "_Bold" : "")}{(cell.IsItalic ? "_Italic" : "")}";
                ElementId textTypeId = GetOrCreateTextNoteType(doc, typeName, cell.IsBold, cell.IsItalic, textSize);

                TextNoteOptions options = new TextNoteOptions
                {
                    HorizontalAlignment = horiz,
                    VerticalAlignment = vert,
                    TypeId = textTypeId
                };

                // Specify model wrap width slightly smaller than cell (accounting for scale)
                double cellWidth = (rightX - leftX) - (2.0 * MmToFeetFactor * scale); // 1mm margin on each side
                if (cellWidth <= 0.001) cellWidth = rightX - leftX;

                TextNote note = TextNote.Create(doc, targetView.Id, insertionPoint, cellWidth, cell.Value, options);
                if (note != null) createdElementIds.Add(note.Id.IntegerValue);
            }

            // 6. Save link in Extensible Storage
            ExcelLinkStorage.SaveLink(targetView, excelPath, sheetName, cellRange, createdElementIds);
        }

        private static DetailCurve DrawLine(Document doc, View view, XYZ p1, XYZ p2)
        {
            if (p1.DistanceTo(p2) < doc.Application.ShortCurveTolerance) return null;
            Line line = Line.CreateBound(p1, p2);
            return doc.Create.NewDetailCurve(view, line);
        }

        private static string MakeUniqueViewName(Document doc, string name)
        {
            string uniqueName = name;
            int counter = 1;
            while (new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Name.Equals(uniqueName, StringComparison.OrdinalIgnoreCase)))
            {
                uniqueName = $"{name}_{counter}";
                counter++;
            }
            return uniqueName;
        }

        private static ElementId GetOrCreateTextNoteType(Document doc, string name, bool bold, bool italic, double textSizeMm)
        {
            TextNoteType existing = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(t => t.Name == name);
            if (existing != null) return existing.Id;

            TextNoteType baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault();

            if (baseType == null) return ElementId.InvalidElementId;

            TextNoteType newType = baseType.Duplicate(name) as TextNoteType;
            if (newType != null)
            {
                Autodesk.Revit.DB.Parameter sizeParam = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                if (sizeParam != null)
                {
                    sizeParam.Set(textSizeMm * MmToFeetFactor);
                }
                Autodesk.Revit.DB.Parameter boldParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD);
                if (boldParam != null)
                {
                    boldParam.Set(bold ? 1 : 0);
                }
                Autodesk.Revit.DB.Parameter italicParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC);
                if (italicParam != null)
                {
                    italicParam.Set(italic ? 1 : 0);
                }
                return newType.Id;
            }
            return baseType.Id;
        }
    }
}
