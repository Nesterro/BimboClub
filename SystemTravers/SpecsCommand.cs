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
    public class SpecsCommand : IExternalCommand
    {
        public class SpecRow
        {
            public string SystemName { get; set; }
            public string Position { get; set; }
            public string Name { get; set; }
            public string Mark { get; set; }
            public string Code { get; set; }
            public string Manufacturer { get; set; }
            public string Unit { get; set; }
            public double Quantity { get; set; }
            public string Weight { get; set; }
            public string Note { get; set; }
            public bool IsLinear { get; set; }
        }

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
                Logger.Log("Запуск команды Умные спецификации по ГОСТ 21.110 (Smart Specs).");

                // 1. Открываем WPF-окно
                SpecsWindow window = new SpecsWindow();
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true && window.IsGenerationRequested)
                {
                    List<BuiltInCategory> categories = new List<BuiltInCategory>();
                    if (window.IncludeDucts) categories.Add(BuiltInCategory.OST_DuctCurves);
                    if (window.IncludePipes) categories.Add(BuiltInCategory.OST_PipeCurves);
                    if (window.IncludeDuctFittings) categories.Add(BuiltInCategory.OST_DuctFitting);
                    if (window.IncludePipeFittings) categories.Add(BuiltInCategory.OST_PipeFitting);
                    if (window.IncludeDuctAccessories) categories.Add(BuiltInCategory.OST_DuctAccessory);
                    if (window.IncludePipeAccessories) categories.Add(BuiltInCategory.OST_PipeAccessory);
                    if (window.IncludeEquipment) categories.Add(BuiltInCategory.OST_MechanicalEquipment);
                    if (window.IncludePlumbing) categories.Add(BuiltInCategory.OST_PlumbingFixtures);

                    List<Element> collectorElements = new List<Element>();
                    foreach (var cat in categories)
                    {
                        FilteredElementCollector col = new FilteredElementCollector(doc)
                            .OfCategory(cat)
                            .WhereElementIsNotElementType();
                        collectorElements.AddRange(col.ToElements());
                    }

                    if (collectorElements.Count == 0)
                    {
                        TaskDialog.Show("Внимание", "Не найдено элементов выбранных категорий в текущем проекте.");
                        return Result.Cancelled;
                    }

                    // Сопоставленные параметры из интерфейса
                    string pPositionName = window.ParamPosition;
                    string pNameName = window.ParamName;
                    string pMarkName = window.ParamMark;
                    string pCodeName = window.ParamCode;
                    string pManufacturerName = window.ParamManufacturer;
                    string pUnitName = window.ParamUnit;
                    string pQuantityName = window.ParamQuantity;
                    string pWeightName = window.ParamWeight;
                    string pNoteName = window.ParamNote;

                    bool writeBack = window.WriteBackToModel;

                    // 2. Режим записи расчетных данных в модель (Write-Back)
                    if (writeBack)
                    {
                        try
                        {
                            using (Transaction tx = new Transaction(doc, "Запись расчетных объемов спецификации"))
                            {
                                tx.Start();
                                int writtenCount = 0;

                                foreach (Element elem in collectorElements)
                                {
                                    bool isLinear = elem is MEPCurve;
                                    double calcQty = 1.0;
                                    string unitStr = "шт.";

                                    if (isLinear)
                                    {
                                        Parameter lenParam = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                                        if (lenParam != null && lenParam.HasValue)
                                        {
                                            calcQty = Math.Round(lenParam.AsDouble() * 0.3048, 2); // футы в метры
                                            unitStr = "м";
                                        }
                                    }

                                    // Записываем в количество
                                    Parameter qParam = elem.LookupParameter(pQuantityName);
                                    if (qParam != null && !qParam.IsReadOnly)
                                    {
                                        SetParamValue(qParam, calcQty);
                                        writtenCount++;
                                    }

                                    // Записываем в единицу измерения
                                    Parameter uParam = elem.LookupParameter(pUnitName);
                                    if (uParam != null && !uParam.IsReadOnly)
                                    {
                                        uParam.Set(unitStr);
                                    }
                                }

                                tx.Commit();
                                Logger.Log($"Успешно записаны параметры спецификации для {writtenCount} элементов модели.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Ошибка при записи расчетных значений в параметры модели", ex);
                        }
                    }

                    // 3. Парсим элементы в строки спецификации
                    List<SpecRow> rawRows = new List<SpecRow>();
                    foreach (Element elem in collectorElements)
                    {
                        try
                        {
                            string systemName = GetElementSystemName(elem);
                            bool isLinear = elem is MEPCurve;

                            // Считываем значения из сопоставленных параметров
                            string positionVal = GetParamString(elem, pPositionName, "—");
                            string nameVal = GetParamString(elem, pNameName);
                            if (nameVal == "—")
                            {
                                // Дефолтное наименование, если параметр пуст
                                nameVal = GetElementFamilyTypeName(elem);
                            }

                            string markVal = GetParamString(elem, pMarkName, "—");
                            string codeVal = GetParamString(elem, pCodeName, "—");
                            string manufacturerVal = GetParamString(elem, pManufacturerName, "—");
                            string unitVal = GetParamString(elem, pUnitName, isLinear ? "м" : "шт.");
                            string weightVal = GetParamString(elem, pWeightName, "—");
                            string noteVal = GetParamString(elem, pNoteName, "—");

                            // Расчет количества для строки
                            double qty = 1.0;
                            Parameter qtyParam = elem.LookupParameter(pQuantityName);
                            if (qtyParam != null && qtyParam.HasValue)
                            {
                                if (qtyParam.StorageType == StorageType.Double) qty = qtyParam.AsDouble();
                                else if (qtyParam.StorageType == StorageType.Integer) qty = qtyParam.AsInteger();
                                else double.TryParse(qtyParam.AsString(), out qty);
                            }
                            else
                            {
                                // Если параметр не заполнен, рассчитываем налету
                                if (isLinear)
                                {
                                    Parameter lenParam = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                                    if (lenParam != null && lenParam.HasValue)
                                    {
                                        qty = lenParam.AsDouble() * 0.3048; // футы в метры
                                    }
                                }
                            }

                            rawRows.Add(new SpecRow
                            {
                                SystemName = systemName,
                                Position = positionVal,
                                Name = nameVal,
                                Mark = markVal,
                                Code = codeVal,
                                Manufacturer = manufacturerVal,
                                Unit = unitVal,
                                Quantity = qty,
                                Weight = weightVal,
                                Note = noteVal,
                                IsLinear = isLinear
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Ошибка парсинга элемента {elem.Id} для ГОСТ спецификации", ex);
                        }
                    }

                    // 4. Группируем строки спецификации по ГОСТ правилам
                    List<SpecRow> groupedRows = new List<SpecRow>();
                    var query = rawRows.AsEnumerable();

                    // Группировка: Система (опц.) + Поз + Наим + Марка + Код + Изготовитель + Ед.изм + Масса + Примечание
                    if (window.GroupBySystem)
                    {
                        var groups = query.GroupBy(r => new { 
                            r.SystemName, r.Position, r.Name, r.Mark, r.Code, r.Manufacturer, r.Unit, r.Weight, r.Note 
                        });
                        foreach (var g in groups)
                        {
                            groupedRows.Add(new SpecRow
                            {
                                SystemName = g.Key.SystemName,
                                Position = g.Key.Position,
                                Name = g.Key.Name,
                                Mark = g.Key.Mark,
                                Code = g.Key.Code,
                                Manufacturer = g.Key.Manufacturer,
                                Unit = g.Key.Unit,
                                Quantity = g.Sum(r => r.Quantity),
                                Weight = g.Key.Weight,
                                Note = g.Key.Note,
                                IsLinear = g.Any(r => r.IsLinear)
                            });
                        }
                    }
                    else
                    {
                        var groups = query.GroupBy(r => new { 
                            r.Position, r.Name, r.Mark, r.Code, r.Manufacturer, r.Unit, r.Weight, r.Note 
                        });
                        foreach (var g in groups)
                        {
                            groupedRows.Add(new SpecRow
                            {
                                SystemName = "Общий комплект",
                                Position = g.Key.Position,
                                Name = g.Key.Name,
                                Mark = g.Key.Mark,
                                Code = g.Key.Code,
                                Manufacturer = g.Key.Manufacturer,
                                Unit = g.Key.Unit,
                                Quantity = g.Sum(r => r.Quantity),
                                Weight = g.Key.Weight,
                                Note = g.Key.Note,
                                IsLinear = g.Any(r => r.IsLinear)
                            });
                        }
                    }

                    // Сортировка строк
                    groupedRows = groupedRows
                        .OrderBy(r => r.SystemName)
                        .ThenBy(r => r.Position)
                        .ThenBy(r => r.Name)
                        .ThenBy(r => r.Mark)
                        .ToList();

                    // 5. Генерируем 9-ти графную XML Excel спецификацию по ГОСТ
                    string excelContent = GenerateXmlExcelContent(groupedRows);
                    File.WriteAllText(window.OutputFilePath, excelContent, System.Text.Encoding.UTF8);

                    Logger.Log($"Успешно экспортировано {groupedRows.Count} строк спецификации по ГОСТ 21.110.");
                    TaskDialog.Show("Успех", $"Спецификация ГОСТ успешно создана!\nОбработано элементов: {collectorElements.Count}\nСтрок спецификации: {groupedRows.Count}\nФайл сохранен: {window.OutputFilePath}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения SpecsCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private string GetElementSystemName(Element elem)
        {
            Parameter sysParam = elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            if (sysParam != null && !string.IsNullOrEmpty(sysParam.AsString()))
            {
                return sysParam.AsString();
            }
            return "Не в системе";
        }

        private string GetElementFamilyTypeName(Element elem)
        {
            if (elem is MEPCurve curve)
            {
                return elem.Name;
            }
            
            ElementId typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element typeElem = elem.Document.GetElement(typeId);
                if (typeElem != null)
                {
                    return $"{elem.Name} ({typeElem.Name})";
                }
            }
            return elem.Name;
        }

        private string GetParamString(Element elem, string paramName, string fallback = "—")
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p != null && p.HasValue)
            {
                if (p.StorageType == StorageType.String)
                {
                    string val = p.AsString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
                else if (p.StorageType == StorageType.Double)
                {
                    return p.AsDouble().ToString("F2");
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    return p.AsInteger().ToString();
                }
                else if (p.StorageType == StorageType.ElementId)
                {
                    ElementId id = p.AsElementId();
                    if (id != ElementId.InvalidElementId)
                    {
                        Element e = elem.Document.GetElement(id);
                        if (e != null) return e.Name;
                    }
                }
            }
            return fallback;
        }

        private void SetParamValue(Parameter p, double val)
        {
            if (p.StorageType == StorageType.Double)
            {
                p.Set(val);
            }
            else if (p.StorageType == StorageType.Integer)
            {
                p.Set((int)Math.Round(val));
            }
            else if (p.StorageType == StorageType.String)
            {
                p.Set(val.ToString("F2"));
            }
        }

        private string GenerateXmlExcelContent(List<SpecRow> rows)
        {
            StringWriter sw = new StringWriter();

            // XML заголовок Spreadsheet 2003
            sw.WriteLine("<?xml version=\"1.0\"?>");
            sw.WriteLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sw.WriteLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sw.WriteLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sw.WriteLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            sw.WriteLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sw.WriteLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");

            // Стили
            sw.WriteLine(" <Styles>");
            sw.WriteLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\">");
            sw.WriteLine("   <Alignment ss:Vertical=\"Center\"/>");
            sw.WriteLine("   <Borders/>");
            sw.WriteLine("   <Font ss:FontName=\"Calibri\" x:CharSet=\"204\" x:Family=\"Swiss\" ss:Size=\"11\" ss:Color=\"#000000\"/>");
            sw.WriteLine("   <Interior/>");
            sw.WriteLine("   <NumberFormat/>");
            sw.WriteLine("   <Protection/>");
            sw.WriteLine("  </Style>");
            
            // Стиль шапки таблицы (темно-алый бренд ГОСТ)
            sw.WriteLine("  <Style ss:ID=\"Header\">");
            sw.WriteLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:WrapText=\"1\"/>");
            sw.WriteLine("   <Borders>");
            sw.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sw.WriteLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sw.WriteLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sw.WriteLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#000000\"/>");
            sw.WriteLine("   </Borders>");
            sw.WriteLine("   <Font ss:FontName=\"Calibri\" x:CharSet=\"204\" ss:Size=\"10\" ss:Color=\"#FFFFFF\" ss:Bold=\"1\"/>");
            sw.WriteLine("   <Interior ss:Color=\"#B30E2D\" ss:Pattern=\"Solid\"/>");
            sw.WriteLine("  </Style>");

            // Стили данных
            sw.WriteLine("  <Style ss:ID=\"DataCell\">");
            sw.WriteLine("   <Alignment ss:Vertical=\"Center\"/>");
            sw.WriteLine("   <Borders>");
            sw.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("   </Borders>");
            sw.WriteLine("   <Font ss:FontName=\"Calibri\" x:CharSet=\"204\" ss:Size=\"10\"/>");
            sw.WriteLine("  </Style>");

            sw.WriteLine("  <Style ss:ID=\"DataCellCenter\">");
            sw.WriteLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sw.WriteLine("   <Borders>");
            sw.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("   </Borders>");
            sw.WriteLine("   <Font ss:FontName=\"Calibri\" x:CharSet=\"204\" ss:Size=\"10\"/>");
            sw.WriteLine("  </Style>");

            sw.WriteLine("  <Style ss:ID=\"DataCellRight\">");
            sw.WriteLine("   <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
            sw.WriteLine("   <Borders>");
            sw.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D0D0\"/>");
            sw.WriteLine("   </Borders>");
            sw.WriteLine("   <Font ss:FontName=\"Calibri\" x:CharSet=\"204\" ss:Size=\"10\"/>");
            sw.WriteLine("  </Style>");

            sw.WriteLine("  <Style ss:ID=\"SystemHeader\">");
            sw.WriteLine("   <Alignment ss:Vertical=\"Center\"/>");
            sw.WriteLine("   <Borders>");
            sw.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"2\" ss:Color=\"#000000\"/>");
            sw.WriteLine("   </Borders>");
            sw.WriteLine("   <Font ss:FontName=\"Calibri\" x:CharSet=\"204\" ss:Size=\"11\" ss:Bold=\"1\" ss:Color=\"#B30E2D\"/>");
            sw.WriteLine("   <Interior ss:Color=\"#F4F4F6\" ss:Pattern=\"Solid\"/>");
            sw.WriteLine("  </Style>");

            sw.WriteLine(" </Styles>");

            // Лист
            sw.WriteLine(" <Worksheet ss:Name=\"Спецификация ГОСТ\">");
            sw.WriteLine("  <Table>");
            // Колонки 9 граф
            sw.WriteLine("   <Column ss:Width=\"40\"/>");  // 1. Поз
            sw.WriteLine("   <Column ss:Width=\"260\"/>"); // 2. Наименование
            sw.WriteLine("   <Column ss:Width=\"140\"/>"); // 3. Марка / обозначение
            sw.WriteLine("   <Column ss:Width=\"100\"/>"); // 4. Код
            sw.WriteLine("   <Column ss:Width=\"140\"/>"); // 5. Изготовитель
            sw.WriteLine("   <Column ss:Width=\"60\"/>");  // 6. Ед.изм
            sw.WriteLine("   <Column ss:Width=\"70\"/>");  // 7. Кол
            sw.WriteLine("   <Column ss:Width=\"70\"/>");  // 8. Масса
            sw.WriteLine("   <Column ss:Width=\"100\"/>"); // 9. Примечание

            // Шапка (высота 32 для WrapText)
            sw.WriteLine("   <Row ss:Height=\"32\">");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Поз.</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Наименование и техническая характеристика</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Тип, марка, обозначение документа</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Код оборудования</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Завод-изготовитель (поставщик)</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Ед. изм.</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Кол.</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Масса ед., кг</Data></Cell>");
            sw.WriteLine("    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">Примечание</Data></Cell>");
            sw.WriteLine("   </Row>");

            // Данные
            string currentSystem = "";
            foreach (var r in rows)
            {
                if (r.SystemName != currentSystem && r.SystemName != "Общий комплект")
                {
                    currentSystem = r.SystemName;
                    sw.WriteLine("   <Row ss:Height=\"24\">");
                    sw.WriteLine($"    <Cell ss:StyleID=\"SystemHeader\" ss:MergeAcross=\"8\"><Data ss:Type=\"String\">СИСТЕМА: {currentSystem}</Data></Cell>");
                    sw.WriteLine("   </Row>");
                }

                sw.WriteLine("   <Row ss:Height=\"20\">");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCellCenter\"><Data ss:Type=\"String\">{r.Position}</Data></Cell>");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCell\"><Data ss:Type=\"String\">{r.Name}</Data></Cell>");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCell\"><Data ss:Type=\"String\">{r.Mark}</Data></Cell>");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCellCenter\"><Data ss:Type=\"String\">{r.Code}</Data></Cell>");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCell\"><Data ss:Type=\"String\">{r.Manufacturer}</Data></Cell>");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCellCenter\"><Data ss:Type=\"String\">{r.Unit}</Data></Cell>");

                if (r.IsLinear)
                {
                    sw.WriteLine($"    <Cell ss:StyleID=\"DataCellRight\"><Data ss:Type=\"Number\">{r.Quantity:F2}</Data></Cell>");
                }
                else
                {
                    sw.WriteLine($"    <Cell ss:StyleID=\"DataCellRight\"><Data ss:Type=\"Number\">{(int)Math.Round(r.Quantity)}</Data></Cell>");
                }

                sw.WriteLine($"    <Cell ss:StyleID=\"DataCellCenter\"><Data ss:Type=\"String\">{r.Weight}</Data></Cell>");
                sw.WriteLine($"    <Cell ss:StyleID=\"DataCell\"><Data ss:Type=\"String\">{r.Note}</Data></Cell>");
                sw.WriteLine("   </Row>");
            }

            sw.WriteLine("  </Table>");
            
            // Настройка листа Excel
            sw.WriteLine("  <WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">");
            sw.WriteLine("   <Selected/>");
            sw.WriteLine("   <ProtectObjects>False</ProtectObjects>");
            sw.WriteLine("   <ProtectScenario>False</ProtectScenario>");
            sw.WriteLine("  </WorksheetOptions>");
            
            sw.WriteLine(" </Worksheet>");
            sw.WriteLine("</Workbook>");

            return sw.ToString();
        }
    }
}
