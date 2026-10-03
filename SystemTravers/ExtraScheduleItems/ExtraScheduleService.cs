using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace BimboClub.ExtraScheduleItems
{
    public static class ExtraScheduleService
    {
        public const string FamilyName = "BCC_Дополнительный_элемент";
        public const string TypeName = "Немоделируемый элемент";
        public const string MarkerComment = "BCC_EXTRA_ITEM";

        // Поддерживаемые категории Revit для немоделируемых элементов
        public static readonly Dictionary<string, BuiltInCategory> SupportedCategories = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
        {
            { "Обобщенные модели", BuiltInCategory.OST_GenericModel },
            { "Арматура воздуховодов", BuiltInCategory.OST_DuctAccessory },
            { "Арматура трубопроводов", BuiltInCategory.OST_PipeAccessory },
            { "Механическое оборудование", BuiltInCategory.OST_MechanicalEquipment },
            { "Сантехнические приборы", BuiltInCategory.OST_PlumbingFixtures },
            { "Электрооборудование", BuiltInCategory.OST_ElectricalEquipment },
            { "Электроприборы", BuiltInCategory.OST_ElectricalFixtures },
            { "Оборудование связи", BuiltInCategory.OST_CommunicationDevices },
            { "Пожарная сигнализация", BuiltInCategory.OST_FireAlarmDevices },
            { "Осветительные приборы", BuiltInCategory.OST_LightingFixtures },
            { "Специальное оборудование", BuiltInCategory.OST_SpecialityEquipment }
        };

        public static BuiltInCategory ResolveBuiltInCategory(string categoryName)
        {
            if (!string.IsNullOrWhiteSpace(categoryName) && SupportedCategories.TryGetValue(categoryName, out var bic))
            {
                return bic;
            }
            return BuiltInCategory.OST_GenericModel;
        }

        public static string GetFamilyNameForCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || categoryName.Equals("Обобщенные модели", StringComparison.OrdinalIgnoreCase))
            {
                return FamilyName;
            }
            string safeName = categoryName.Replace(" ", "_");
            return $"BCC_Доп_{safeName}";
        }

        // Стандартные имена параметров ADSK с альтернативами
        public static readonly string[] GroupParamNames = { "ADSK_Группирование", "ADSK_Раздел проекта", "Раздел проекта", "Группирование" };
        public static readonly string[] PositionParamNames = { "ADSK_Позиция", "Позиция", "Номер позиции" };
        public static readonly string[] NameParamNames = { "ADSK_Наименование", "Наименование", "Описание" };
        public static readonly string[] MarkParamNames = { "ADSK_Марка", "ADSK_Обозначение", "Марка", "Обозначение" };
        public static readonly string[] CodeParamNames = { "ADSK_Код изделия", "Код изделия", "Код" };
        public static readonly string[] ManufacturerParamNames = { "ADSK_Завод-изготовитель", "ADSK_Изготовитель", "Завод-изготовитель", "Изготовитель" };
        public static readonly string[] UnitParamNames = {
            "ADSK_Единицы измерения",
            "ADSK_Единица измерения",
            "ADSK_Единицы измрения",
            "ADSK_Единица измрения",
            "Единицы измерения",
            "Единица измерения",
            "Ед. изм.",
            "Ед.изм."
        };
        public static readonly string[] CountParamNames = { "ADSK_Количество", "Количество", "Число" };
        public static readonly string[] WeightParamNames = { "ADSK_Масса", "Масса", "Вес" };
        public static readonly string[] NoteParamNames = { "ADSK_Примечание", "Примечание", "Примечания" };

        private class AdskParamDef
        {
            public string Name { get; set; }
            public Guid Guid { get; set; }
            public bool IsNumber { get; set; }
        }

        private static readonly AdskParamDef[] AdskParams = new[]
        {
            new AdskParamDef { Name = "ADSK_Группирование", Guid = new Guid("2b5ec4e0-7168-4509-9ec6-896894ea2613"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Позиция", Guid = new Guid("6a2e4c49-0144-4861-8284-a134eb9fa388"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Наименование", Guid = new Guid("e6e0f5cd-3e26-407b-9993-4a11be2454be"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Марка", Guid = new Guid("809e51c8-c689-49c9-a548-8cf9c9162985"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Код изделия", Guid = new Guid("a22efc36-7c9b-4e08-9dfc-91aa0350d755"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Завод-изготовитель", Guid = new Guid("aa041857-96a2-4a0b-8526-cb17a5be5573"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Единицы измерения", Guid = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Единица измерения", Guid = new Guid("5526cb60-d264-4e4f-b620-333e144a9910"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Единицы измрения", Guid = new Guid("f12a32c4-0692-411a-a1b7-a3cf2d0e82c5"), IsNumber = false },
            new AdskParamDef { Name = "ADSK_Количество", Guid = new Guid("8d0092c6-3023-455b-80fb-b0c4119d8504"), IsNumber = true },
            new AdskParamDef { Name = "ADSK_Масса", Guid = new Guid("4b71f9cf-ff58-45f8-b32c-7b447432c668"), IsNumber = true },
            new AdskParamDef { Name = "ADSK_Примечание", Guid = new Guid("e2a4beaa-43e7-4b77-8fa0-681e18ca4743"), IsNumber = false }
        };

        /// <summary>
        /// Сканирование документа Revit на ранее созданные немоделируемые элементы во всех поддерживаемых категориях
        /// </summary>
        public static List<ExtraScheduleItem> ScanExistingItems(Document doc)
        {
            var result = new List<ExtraScheduleItem>();
            try
            {
                var seenIds = new HashSet<int>();

                foreach (var kvp in SupportedCategories)
                {
                    FilteredElementCollector collector;
                    try
                    {
                        collector = new FilteredElementCollector(doc)
                            .OfCategory(kvp.Value)
                            .WhereElementIsNotElementType();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Element elem in collector)
                    {
                        if (elem == null) continue;
                        int idVal = elem.Id.IntegerValue;
                        if (seenIds.Contains(idVal)) continue;

                        if (IsExtraScheduleElement(elem))
                        {
                            seenIds.Add(idVal);
                            var item = new ExtraScheduleItem
                            {
                                Id = idVal,
                                IsNew = false,
                                IsModified = false,
                                CategoryName = kvp.Key,
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
                string famName = fi.Symbol.Family.Name ?? "";
                if (famName.Equals(FamilyName, StringComparison.OrdinalIgnoreCase) ||
                    famName.StartsWith("BCC_Доп", StringComparison.OrdinalIgnoreCase))
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

            if (itemsToSync == null) itemsToSync = new List<ExtraScheduleItem>();

            // 0. Предобработка: проверяем семейства и параметры ДО открытия транзакции в doc
            EnsureFamiliesAndBindings(doc, itemsToSync);

            using (Transaction t = new Transaction(doc, "Синхронизация доп. элементов спецификации"))
            {
                t.Start();

                // 0.1 Гарантируем, что в проекте параметры единиц измерения привязаны как параметры ЭКЗЕМПЛЯРА
                foreach (var kvp in SupportedCategories)
                {
                    EnsureProjectInstanceBindings(doc, kvp.Value);
                }

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

                // 2. Кэш типоразмеров семейств по категориям
                var symbolsCache = new Dictionary<string, FamilySymbol>(StringComparer.OrdinalIgnoreCase);

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
                            string catName = string.IsNullOrWhiteSpace(item.CategoryName) ? "Обобщенные модели" : item.CategoryName;
                            BuiltInCategory expectedBic = ResolveBuiltInCategory(catName);
                            if (targetElem.Category == null || targetElem.Category.Id.IntegerValue != (int)expectedBic)
                            {
                                doc.Delete(targetElem.Id);
                                targetElem = null;
                            }
                            else
                            {
                                updated++;
                            }
                        }
                    }

                    if (targetElem == null)
                    {
                        // Определяем семейство под категорию элемента
                        string catName = string.IsNullOrWhiteSpace(item.CategoryName) ? "Обобщенные модели" : item.CategoryName;
                        if (!symbolsCache.TryGetValue(catName, out FamilySymbol symbol) || symbol == null)
                        {
                            symbol = GetOrCreateFamilySymbol(doc, catName);
                            if (symbol != null)
                            {
                                if (!symbol.IsActive)
                                {
                                    symbol.Activate();
                                    doc.Regenerate();
                                }
                                symbolsCache[catName] = symbol;
                            }
                        }

                        if (symbol == null)
                        {
                            // Запасной вариант: Обобщенные модели
                            if (!symbolsCache.TryGetValue("Обобщенные модели", out symbol) || symbol == null)
                            {
                                symbol = GetOrCreateFamilySymbol(doc, "Обобщенные модели");
                                if (symbol != null && !symbol.IsActive)
                                {
                                    symbol.Activate();
                                    doc.Regenerate();
                                }
                                if (symbol != null) symbolsCache["Обобщенные модели"] = symbol;
                            }
                        }

                        if (symbol == null)
                        {
                            throw new InvalidOperationException($"Не удалось найти или создать семейство для категории '{catName}'.");
                        }

                        // Создание нового экземпляра
                        XYZ location = new XYZ(baseX + index * 0.1, baseY, baseZ);
                        FamilyInstance instance = null;
                        try
                        {
                            instance = doc.Create.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
                            if (instance != null && level != null)
                            {
                                var pLevel = instance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                                if (pLevel != null && !pLevel.IsReadOnly)
                                {
                                    pLevel.Set(level.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Ошибка размещения экземпляра немоделируемого элемента", ex);
                        }

                        if (instance == null) continue;

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
            SetParam(elem, UnitParamNames, item.Unit, setAllMatches: true);
            SetParam(elem, CountParamNames, item.Count);
            SetParam(elem, WeightParamNames, item.Weight);
            SetParam(elem, NoteParamNames, item.Note);
        }

        public static FamilySymbol GetOrCreateFamilySymbol(Document doc, string categoryName = null)
        {
            string targetCatName = string.IsNullOrWhiteSpace(categoryName) ? "Обобщенные модели" : categoryName;
            string famName = GetFamilyNameForCategory(targetCatName);
            BuiltInCategory bic = ResolveBuiltInCategory(targetCatName);

            // 1. Поиск существующего семейства по имени и категории
            var symbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(bic)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Family != null && string.Equals(s.Family.Name, famName, StringComparison.OrdinalIgnoreCase));

            if (symbol != null) return symbol;

            // 1.1 Поиск семейства по имени среди всех семейств проекта
            var family = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f != null && string.Equals(f.Name, famName, StringComparison.OrdinalIgnoreCase));
            if (family != null)
            {
                var symId = family.GetFamilySymbolIds().FirstOrDefault();
                if (symId != null && doc.GetElement(symId) is FamilySymbol fs)
                {
                    return fs;
                }
            }

            // 2. Попытка загрузить семейство из файла
            string rfaPath = EnsureFamilyFile(doc.Application, famName, bic);
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

            // 3. Fallback: поиск любого существующего FamilySymbol данной категории
            var fallbackSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(bic)
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            if (fallbackSymbol != null)
            {
                try
                {
                    var newSymbol = fallbackSymbol.Duplicate(TypeName) as FamilySymbol;
                    if (newSymbol != null) return newSymbol;
                }
                catch { }
                return fallbackSymbol;
            }

            // 4. Если категория специфическая и не удалось создать, откатываемся на Обобщенные модели
            if (bic != BuiltInCategory.OST_GenericModel)
            {
                return GetOrCreateFamilySymbol(doc, "Обобщенные модели");
            }

            return null;
        }

        public class ExtraScheduleFamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }

        private static string EnsureAdskSharedParamFile()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BimboClub",
                "Families"
            );
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

            string filePath = Path.Combine(appData, "BCC_ADSK_SharedParams.txt");
            var sb = new StringBuilder();
            sb.AppendLine("# This is a Revit shared parameter file.");
            sb.AppendLine("# Do not edit manually.");
            sb.AppendLine("*META\tVERSION\tMINVERSION");
            sb.AppendLine("META\t2\t1");
            sb.AppendLine("*GROUP\tID\tNAME");
            sb.AppendLine("GROUP\t1\tADSK");
            sb.AppendLine("*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE");
            foreach (var p in AdskParams)
            {
                string dt = p.IsNumber ? "NUMBER" : "TEXT";
                sb.AppendLine($"PARAM\t{p.Guid}\t{p.Name}\t{dt}\t\t1\t1\t\t1\t0");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.Unicode);
            return filePath;
        }

        private static FamilyParameter EnsureFamilyParameter(
            FamilyManager famMgr,
            DefinitionGroup grp,
            string paramName,
            ForgeTypeId specTypeId,
            Guid paramGuid,
            ref bool modified,
            bool isInstance = true)
        {
            FamilyParameter fp = famMgr.get_Parameter(paramName);
            if (fp != null)
            {
                if (!fp.IsInstance && isInstance)
                {
                    try
                    {
                        famMgr.MakeInstance(fp);
                        modified = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Не удалось сделать параметр {paramName} экземпляром: {ex.Message}", "WARN");
                    }
                }
                return fp;
            }

            try
            {
                Definition def = grp?.Definitions?.get_Item(paramName);
                if (def == null && grp != null)
                {
                    ExternalDefinitionCreationOptions extOpts = new ExternalDefinitionCreationOptions(paramName, specTypeId)
                    {
                        GUID = paramGuid != Guid.Empty ? paramGuid : Guid.NewGuid(),
                        UserModifiable = true
                    };
                    def = grp.Definitions.Create(extOpts);
                }

                if (def is ExternalDefinition extDef)
                {
#if NET48
#pragma warning disable CS0618
                    fp = famMgr.AddParameter(extDef, BuiltInParameterGroup.PG_DATA, isInstance: isInstance);
#pragma warning restore CS0618
#else
                    fp = famMgr.AddParameter(extDef, GroupTypeId.Data, isInstance: isInstance);
#endif
                    modified = true;
                }
                else
                {
                    fp = famMgr.AddParameter(paramName, GroupTypeId.Data, specTypeId, isInstance: isInstance);
                    modified = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка добавления параметра {paramName} в семейство: {ex.Message}", "WARN");
            }

            return fp;
        }

        private static void EnsureFamilyParameters(Autodesk.Revit.ApplicationServices.Application app, FamilyManager famMgr, ref bool modified)
        {
            if (famMgr == null) return;

            // 1. Если параметры уже есть в семействе как параметры ТИПА — преобразуем в ЭКЗЕМПЛЯР
            foreach (FamilyParameter fp in famMgr.Parameters)
            {
                if (fp == null || fp.Definition == null) continue;
                string pName = fp.Definition.Name;

                bool isTarget = UnitParamNames.Any(u => string.Equals(u, pName, StringComparison.OrdinalIgnoreCase))
                    || AdskParams.Any(ap => string.Equals(ap.Name, pName, StringComparison.OrdinalIgnoreCase));

                if (isTarget && !fp.IsInstance)
                {
                    try
                    {
                        famMgr.MakeInstance(fp);
                        modified = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Не удалось сделать параметр {pName} экземпляром: {ex.Message}", "WARN");
                    }
                }
            }

            // 2. Создаем/открываем файл общих параметров ADSK
            string origShared = null;
            try
            {
                origShared = app.SharedParametersFilename;
            }
            catch { }

            try
            {
                string sharedPath = EnsureAdskSharedParamFile();
                app.SharedParametersFilename = sharedPath;
                DefinitionFile defFile = app.OpenSharedParameterFile();
                if (defFile != null)
                {
                    DefinitionGroup defGroup = defFile.Groups.get_Item("ADSK") ?? defFile.Groups.Create("ADSK");

                    foreach (var pDef in AdskParams)
                    {
                        FamilyParameter fp = famMgr.get_Parameter(pDef.Name);
                        if (fp != null)
                        {
                            if (!fp.IsInstance)
                            {
                                try
                                {
                                    famMgr.MakeInstance(fp);
                                    modified = true;
                                }
                                catch { }
                            }
                            continue;
                        }

                        ForgeTypeId specTypeId = pDef.IsNumber ? SpecTypeId.Number : SpecTypeId.String.Text;
                        EnsureFamilyParameter(famMgr, defGroup, pDef.Name, specTypeId, pDef.Guid, ref modified, isInstance: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка настройки общих параметров семейства", ex);
            }
            finally
            {
                try
                {
                    if (origShared != null) app.SharedParametersFilename = origShared;
                }
                catch { }
            }

            // 3. Fallback: гарантируем наличие параметров единиц измерения как параметров экземпляра
            try
            {
                bool hasUnitParam = false;
                foreach (FamilyParameter fp in famMgr.Parameters)
                {
                    if (fp != null && fp.Definition != null &&
                        (string.Equals(fp.Definition.Name, "ADSK_Единицы измерения", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(fp.Definition.Name, "ADSK_Единица измерения", StringComparison.OrdinalIgnoreCase)))
                    {
                        hasUnitParam = true;
                        if (!fp.IsInstance)
                        {
                            famMgr.MakeInstance(fp);
                            modified = true;
                        }
                    }
                }

                if (!hasUnitParam)
                {
                    famMgr.AddParameter("ADSK_Единицы измерения", GroupTypeId.Data, SpecTypeId.String.Text, isInstance: true);
                    modified = true;
                }
            }
            catch { }
        }

        private static void EnsureFamiliesAndBindings(Document doc, IEnumerable<ExtraScheduleItem> items)
        {
            if (doc == null) return;

            var catNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (items != null)
            {
                foreach (var item in items)
                {
                    string cat = string.IsNullOrWhiteSpace(item.CategoryName) ? "Обобщенные модели" : item.CategoryName;
                    catNames.Add(cat);
                }
            }
            if (catNames.Count == 0) catNames.Add("Обобщенные модели");

            foreach (string catName in catNames)
            {
                string famName = GetFamilyNameForCategory(catName);
                BuiltInCategory bic = ResolveBuiltInCategory(catName);

                // 1. Убеждаемся, что файл семейства на диске существует и имеет параметры экземпляра
                EnsureFamilyFile(doc.Application, famName, bic);

                // 2. Если семейство уже загружено в документ — проверяем, чтобы параметры не были параметрами ТИПА
                try
                {
                    var family = new FilteredElementCollector(doc)
                        .OfClass(typeof(Family))
                        .Cast<Family>()
                        .FirstOrDefault(f => f != null && string.Equals(f.Name, famName, StringComparison.OrdinalIgnoreCase));

                    if (family != null && family.IsEditable)
                    {
                        bool hasTypeUnitParam = false;
                        foreach (ElementId symId in family.GetFamilySymbolIds())
                        {
                            if (doc.GetElement(symId) is FamilySymbol fs)
                            {
                                foreach (string uName in UnitParamNames)
                                {
                                    Parameter p = fs.LookupParameter(uName);
                                    if (p != null)
                                    {
                                        hasTypeUnitParam = true;
                                        break;
                                    }
                                }
                            }
                            if (hasTypeUnitParam) break;
                        }

                        if (hasTypeUnitParam)
                        {
                            Document famDoc = doc.EditFamily(family);
                            if (famDoc != null)
                            {
                                bool modified = false;
                                using (Transaction tf = new Transaction(famDoc, "BimboClub: Параметры экземпляра"))
                                {
                                    tf.Start();
                                    EnsureFamilyParameters(doc.Application, famDoc.FamilyManager, ref modified);
                                    tf.Commit();
                                }

                                if (modified)
                                {
                                    famDoc.LoadFamily(doc, new ExtraScheduleFamilyLoadOptions());
                                }
                                famDoc.Close(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Ошибка проверки параметров семейства {famName}: {ex.Message}", "WARN");
                }
            }
        }

        public static void EnsureProjectInstanceBindings(Document doc, BuiltInCategory bic)
        {
            if (doc == null) return;
            try
            {
                BindingMap bindMap = doc.ParameterBindings;
                if (bindMap == null) return;

                Category targetCat = null;
                try
                {
                    targetCat = doc.Settings.Categories.get_Item(bic);
                }
                catch { }

                if (targetCat == null) return;

                // 1. Проверяем существующие привязки проекта: если параметр единиц измерения привязан как параметр ТИПА — перепривязываем к ЭКЗЕМПЛЯРУ!
                DefinitionBindingMapIterator it = bindMap.ForwardIterator();
                var reinsertList = new List<Tuple<Definition, InstanceBinding>>();

                while (it.MoveNext())
                {
                    Definition def = it.Key;
                    if (def == null) continue;

                    bool isUnitParam = UnitParamNames.Any(u => string.Equals(u, def.Name, StringComparison.OrdinalIgnoreCase));
                    if (!isUnitParam) continue;

                    Binding binding = it.Current as Binding;
                    if (binding is TypeBinding tb)
                    {
                        // Привязан как параметр типа в проекте — делаем параметром экземпляра!
                        CategorySet catSet = tb.Categories ?? doc.Application.Create.NewCategorySet();
                        if (!catSet.Contains(targetCat))
                        {
                            catSet.Insert(targetCat);
                        }
                        InstanceBinding instBinding = doc.Application.Create.NewInstanceBinding(catSet);
                        reinsertList.Add(Tuple.Create(def, instBinding));
                    }
                    else if (binding is InstanceBinding ib)
                    {
                        CategorySet catSet = ib.Categories;
                        if (catSet != null && !catSet.Contains(targetCat))
                        {
                            catSet.Insert(targetCat);
                            reinsertList.Add(Tuple.Create(def, ib));
                        }
                    }
                }

                foreach (var item in reinsertList)
                {
                    try
                    {
#if NET48
#pragma warning disable CS0618
                        bindMap.ReInsert(item.Item1, item.Item2, BuiltInParameterGroup.PG_DATA);
#pragma warning restore CS0618
#else
                        bindMap.ReInsert(item.Item1, item.Item2, GroupTypeId.Data);
#endif
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Ошибка перепривязки {item.Item1.Name} к экземпляру: {ex.Message}", "WARN");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в EnsureProjectInstanceBindings", ex);
            }
        }

        private static string EnsureFamilyFile(Autodesk.Revit.ApplicationServices.Application app, string famName, BuiltInCategory bic)
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BimboClub",
                "Families"
            );
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

            string targetPath = Path.Combine(appData, $"{famName}.rfa");
            if (File.Exists(targetPath))
            {
                // Проверяем существующий файл семейства и убеждаемся, что параметры являются параметрами экземпляра
                try
                {
                    Document existingFamDoc = app.OpenDocumentFile(targetPath);
                    if (existingFamDoc != null)
                    {
                        bool modified = false;
                        using (Transaction tParams = new Transaction(existingFamDoc, "Проверка параметров экземпляра"))
                        {
                            tParams.Start();
                            EnsureFamilyParameters(app, existingFamDoc.FamilyManager, ref modified);
                            tParams.Commit();
                        }

                        if (modified)
                        {
                            existingFamDoc.Save();
                        }
                        existingFamDoc.Close(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Проверка существующего файла семейства {famName}: {ex.Message}", "DEBUG");
                }
                return targetPath;
            }

            // Поиск шаблона Metric Generic Model.rft
            string templatePath = FindGenericModelTemplate(app.VersionNumber);
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                try
                {
                    Document famDoc = app.NewFamilyDocument(templatePath);
                    if (famDoc != null)
                    {
                        if (bic != BuiltInCategory.OST_GenericModel && famDoc.OwnerFamily != null)
                        {
                            try
                            {
                                using (Transaction tCat = new Transaction(famDoc, "Категория"))
                                {
                                    tCat.Start();
                                    famDoc.OwnerFamily.FamilyCategoryId = new ElementId(bic);
                                    tCat.Commit();
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Не удалось назначить категорию {bic} семейству {famName}: {ex.Message}", "WARN");
                            }
                        }

                        // Добавляем параметры ADSK как параметры ЭКЗЕМПЛЯРА
                        bool modified = false;
                        using (Transaction tParams = new Transaction(famDoc, "Параметры ADSK (экземпляр)"))
                        {
                            tParams.Start();
                            EnsureFamilyParameters(app, famDoc.FamilyManager, ref modified);
                            tParams.Commit();
                        }

                        SaveAsOptions opt = new SaveAsOptions { OverwriteExistingFile = true };
                        famDoc.SaveAs(targetPath, opt);
                        famDoc.Close(false);
                        return targetPath;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка генерации семейства " + famName, ex);
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

        public static bool SetParam(Element elem, string[] paramNames, object value, bool setAllMatches = false)
        {
            if (elem == null || value == null) return false;

            bool anySet = false;
            foreach (string name in paramNames)
            {
                Parameter p = elem.LookupParameter(name);
                if (p != null && !p.IsReadOnly)
                {
                    // ВАЖНО: Если записываем в экземпляр (FamilyInstance), параметр не должен принадлежать FamilySymbol (параметр типа)
                    if (elem is FamilyInstance && p.Element is FamilySymbol)
                    {
                        continue;
                    }

                    try
                    {
                        if (p.StorageType == StorageType.String)
                        {
                            p.Set(value.ToString());
                            anySet = true;
                            if (!setAllMatches) return true;
                        }
                        else if (p.StorageType == StorageType.Double)
                        {
                            if (value is double d) { p.Set(d); anySet = true; if (!setAllMatches) return true; }
                            else if (value is int i) { p.Set((double)i); anySet = true; if (!setAllMatches) return true; }
                            else if (double.TryParse(value.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                            {
                                p.Set(parsed);
                                anySet = true;
                                if (!setAllMatches) return true;
                            }
                        }
                        else if (p.StorageType == StorageType.Integer)
                        {
                            if (value is int i) { p.Set(i); anySet = true; if (!setAllMatches) return true; }
                            else if (int.TryParse(value.ToString(), out int parsed))
                            {
                                p.Set(parsed);
                                anySet = true;
                                if (!setAllMatches) return true;
                            }
                        }
                    }
                    catch { }
                }
            }

            return anySet;
        }

        #endregion
    }
}
