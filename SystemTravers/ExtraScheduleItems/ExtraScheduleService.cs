using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace BimboClub.ExtraScheduleItems
{
    public static class ExtraScheduleService
    {
        public const string FamilyName = "BCC_Дополнительный_элемент";
        public const string TypeName = "Немоделируемый элемент";
        public const string MarkerComment = "BCC_EXTRA_ITEM";

        // Стандартные имена параметров ADSK с альтернативами
        public static readonly string[] GroupParamNames = { "ADSK_Группирование", "ADSK_Раздел проекта", "Раздел проекта", "Группирование" };
        public static readonly string[] PositionParamNames = { "ADSK_Позиция", "Позиция", "Номер позиции" };
        public static readonly string[] NameParamNames = { "ADSK_Наименование", "Наименование", "Описание" };
        public static readonly string[] MarkParamNames = { "ADSK_Марка", "ADSK_Обозначение", "Марка", "Обозначение" };
        public static readonly string[] CodeParamNames = { "ADSK_Код изделия", "Код изделия", "Код" };
        public static readonly string[] ManufacturerParamNames = { "ADSK_Завод-изготовитель", "ADSK_Изготовитель", "Завод-изготовитель", "Изготовитель" };
        public static readonly string[] UnitParamNames = { "ADSK_Единица измерения", "Единица измерения", "Ед. изм.", "Ед.изм." };
        public static readonly string[] CountParamNames = { "ADSK_Количество", "Количество", "Число" };
        public static readonly string[] WeightParamNames = { "ADSK_Масса", "Масса", "Вес" };
        public static readonly string[] NoteParamNames = { "ADSK_Примечание", "Примечание", "Примечания" };

        /// <summary>
        /// Сканирование документа Revit на ранее созданные немоделируемые элементы
        /// </summary>
        public static List<ExtraScheduleItem> ScanExistingItems(Document doc)
        {
            var result = new List<ExtraScheduleItem>();
            try
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType();

                foreach (Element elem in collector)
                {
                    if (IsExtraScheduleElement(elem))
                    {
                        var item = new ExtraScheduleItem
                        {
                            Id = elem.Id.IntegerValue,
                            IsNew = false,
                            IsModified = false,
                            Group = GetStringParam(elem, GroupParamNames),
                            Position = GetStringParam(elem, PositionParamNames),
                            Name = GetStringParam(elem, NameParamNames),
                            Mark = GetStringParam(elem, MarkParamNames),
                            Code = GetStringParam(elem, CodeParamNames),
                            Manufacturer = GetStringParam(elem, ManufacturerParamNames),
                            Unit = GetStringParam(elem, UnitParamNames, "шт"),
                            Count = GetDoubleParam(elem, CountParamNames, 1.0),
                            Weight = GetDoubleParam(elem, WeightParamNames, 0.0),
                            Note = GetStringParam(elem, NoteParamNames)
                        };

                        // Если имя пустое - попробуем взять из имени типа
                        if (string.IsNullOrWhiteSpace(item.Name))
                        {
                            var typeElem = doc.GetElement(elem.GetTypeId());
                            if (typeElem != null)
                            {
                                item.Name = GetStringParam(typeElem, NameParamNames);
                            }
                        }

                        result.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка при сканировании немоделируемых элементов", ex);
            }

            return result;
        }

        public static bool IsExtraScheduleElement(Element elem)
        {
            if (elem == null) return false;

            // 1. Проверка комментария-маркера
            var commentParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentParam != null && commentParam.HasValue)
            {
                string val = commentParam.AsString() ?? "";
                if (val.Contains(MarkerComment)) return true;
            }

            // 2. Проверка имени семейства
            if (elem is FamilyInstance fi && fi.Symbol?.Family != null)
            {
                if (fi.Symbol.Family.Name.Equals(FamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Синхронизация списка элементов со спецификацией/моделью Revit
        /// </summary>
        public static (int created, int updated, int deleted) SyncWithModel(
            Document doc,
            List<ExtraScheduleItem> itemsToSync,
            List<int> deletedIds)
        {
            int created = 0;
            int updated = 0;
            int deleted = 0;

            using (Transaction t = new Transaction(doc, "Синхронизация доп. элементов спецификации"))
            {
                t.Start();

                // 1. Удаление элементов, удаленных пользователем
                if (deletedIds != null)
                {
                    foreach (int idVal in deletedIds)
                    {
                        if (idVal > 0)
                        {
                            ElementId eid = new ElementId(idVal);
                            Element elem = doc.GetElement(eid);
                            if (elem != null)
                            {
                                doc.Delete(eid);
                                deleted++;
                            }
                        }
                    }
                }

                // 2. Получение или создание типоразмера семейства
                FamilySymbol symbol = GetOrCreateFamilySymbol(doc);
                if (symbol == null)
                {
                    throw new InvalidOperationException("Не удалось найти или создать семейство для немоделируемых элементов.");
                }

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                // Уровень для привязки (любой первый уровень)
                Level level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();

                // Базовая координата размещения невидимых элементов:
                // Размещаем в безопасной координате (0, 0, -50 футов) со сдвигом по X,
                // чтобы не пересекать геометрию проекта и не мешать привязкам
                double baseX = 0.0;
                double baseY = 0.0;
                double baseZ = -50.0; // под нулем проекта

                // 3. Создание новых и обновление существующих
                int index = 0;
                foreach (var item in itemsToSync)
                {
                    Element targetElem = null;

                    if (item.Id > 0)
                    {
                        // Существующий элемент
                        targetElem = doc.GetElement(new ElementId(item.Id));
                        if (targetElem != null)
                        {
                            updated++;
                        }
                    }

                    if (targetElem == null)
                    {
                        // Создание нового экземпляра
                        XYZ location = new XYZ(baseX + index * 0.1, baseY, baseZ);
                        FamilyInstance instance;
                        if (level != null)
                        {
                            instance = doc.Create.NewFamilyInstance(location, symbol, level, StructuralType.NonStructural);
                        }
                        else
                        {
                            instance = doc.Create.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
                        }

                        // Установка метки плагина в Comments
                        var commentParam = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (commentParam != null && !commentParam.IsReadOnly)
                        {
                            commentParam.Set(MarkerComment);
                        }

                        targetElem = instance;
                        item.Id = instance.Id.IntegerValue;
                        item.IsNew = false;
                        created++;
                    }

                    // Запись параметров ADSK
                    ApplyParametersToElement(targetElem, item);
                    item.IsModified = false;
                    index++;
                }

                t.Commit();
            }

            return (created, updated, deleted);
        }

        private static void ApplyParametersToElement(Element elem, ExtraScheduleItem item)
        {
            if (elem == null || item == null) return;

            SetParam(elem, GroupParamNames, item.Group);
            SetParam(elem, PositionParamNames, item.Position);
            SetParam(elem, NameParamNames, item.Name);
            SetParam(elem, MarkParamNames, item.Mark);
            SetParam(elem, CodeParamNames, item.Code);
            SetParam(elem, ManufacturerParamNames, item.Manufacturer);
            SetParam(elem, UnitParamNames, item.Unit);
            SetParam(elem, CountParamNames, item.Count);
            SetParam(elem, WeightParamNames, item.Weight);
            SetParam(elem, NoteParamNames, item.Note);
        }

        public static FamilySymbol GetOrCreateFamilySymbol(Document doc)
        {
            // 1. Поиск существующего семейства BCC_Дополнительный_элемент
            var symbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Family.Name.Equals(FamilyName, StringComparison.OrdinalIgnoreCase));

            if (symbol != null) return symbol;

            // 2. Попытка загрузить семейство из шаблона или временного файла
            string rfaPath = EnsureFamilyFile(doc.Application);
            if (!string.IsNullOrEmpty(rfaPath) && File.Exists(rfaPath))
            {
                Family loadedFam = null;
                if (doc.LoadFamily(rfaPath, out loadedFam) && loadedFam != null)
                {
                    var symbolId = loadedFam.GetFamilySymbolIds().FirstOrDefault();
                    if (symbolId != null)
                    {
                        return doc.GetElement(symbolId) as FamilySymbol;
                    }
                }
            }

            // 3. Fallback: если не удалось создать новое семейство, используем любой доступный FamilySymbol GenericModel
            var fallbackSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            if (fallbackSymbol != null)
            {
                // Дублируем тип, чтобы отделить служебные элементы
                try
                {
                    var newSymbol = fallbackSymbol.Duplicate(TypeName) as FamilySymbol;
                    if (newSymbol != null) return newSymbol;
                }
                catch { }
                return fallbackSymbol;
            }

            return null;
        }

        private static string EnsureFamilyFile(Autodesk.Revit.ApplicationServices.Application app)
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BimboClub",
                "Families"
            );
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

            string targetPath = Path.Combine(appData, $"{FamilyName}.rfa");
            if (File.Exists(targetPath)) return targetPath;

            // Поиск шаблона Metric Generic Model.rft
            string templatePath = FindGenericModelTemplate(app.VersionNumber);
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                try
                {
                    Document famDoc = app.NewFamilyDocument(templatePath);
                    if (famDoc != null)
                    {
                        // Сохраняем пустое семейство (без геометрии)
                        SaveAsOptions opt = new SaveAsOptions { OverwriteExistingFile = true };
                        famDoc.SaveAs(targetPath, opt);
                        famDoc.Close(false);
                        return targetPath;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка генерации семейства " + FamilyName, ex);
                }
            }

            return null;
        }

        private static string FindGenericModelTemplate(string version)
        {
            string commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] searchDirs = {
                Path.Combine(commonData, "Autodesk", $"RVT {version}", "Family Templates", "English"),
                Path.Combine(commonData, "Autodesk", $"RVT {version}", "Family Templates", "Russian"),
                Path.Combine(commonData, "Autodesk", $"RVT {version}", "Family Templates", "English-Imperial"),
                Path.Combine(commonData, "Autodesk", "RVT 2022", "Family Templates", "English"),
                Path.Combine(commonData, "Autodesk", "RVT 2024", "Family Templates", "English")
            };

            foreach (string dir in searchDirs)
            {
                if (Directory.Exists(dir))
                {
                    string metric = Path.Combine(dir, "Metric Generic Model.rft");
                    if (File.Exists(metric)) return metric;

                    string russian = Path.Combine(dir, "Метрическая система, типовая модель.rft");
                    if (File.Exists(russian)) return russian;

                    string imperial = Path.Combine(dir, "Generic Model.rft");
                    if (File.Exists(imperial)) return imperial;
                }
            }

            return null;
        }

        #region Вспомогательные методы чтения и записи параметров

        public static string GetStringParam(Element elem, string[] paramNames, string defaultValue = "")
        {
            if (elem == null) return defaultValue;

            foreach (string name in paramNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p != null && p.HasValue)
                {
                    string val = p.AsString();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }

            return defaultValue;
        }

        public static double GetDoubleParam(Element elem, string[] paramNames, double defaultValue = 0.0)
        {
            if (elem == null) return defaultValue;

            foreach (string name in paramNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p != null && p.HasValue)
                {
                    if (p.StorageType == StorageType.Double)
                    {
                        return p.AsDouble();
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        return p.AsInteger();
                    }
                    else if (p.StorageType == StorageType.String)
                    {
                        string s = p.AsString()?.Replace(',', '.');
                        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }

            return defaultValue;
        }

        public static bool SetParam(Element elem, string[] paramNames, object value)
        {
            if (elem == null || value == null) return false;

            foreach (string name in paramNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p != null && !p.IsReadOnly)
                {
                    try
                    {
                        if (p.StorageType == StorageType.String)
                        {
                            p.Set(value.ToString());
                            return true;
                        }
                        else if (p.StorageType == StorageType.Double)
                        {
                            if (value is double d) { p.Set(d); return true; }
                            if (value is int i) { p.Set((double)i); return true; }
                            if (double.TryParse(value.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                            {
                                p.Set(parsed);
                                return true;
                            }
                        }
                        else if (p.StorageType == StorageType.Integer)
                        {
                            if (value is int i) { p.Set(i); return true; }
                            if (int.TryParse(value.ToString(), out int parsed))
                            {
                                p.Set(parsed);
                                return true;
                            }
                        }
                    }
                    catch { }
                }
            }

            return false;
        }

        #endregion
    }
}
