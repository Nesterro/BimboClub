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

        public static readonly string[] GroupParamNames = ExtraScheduleParameterSchema.Fields[0].Names;
        public static readonly string[] PositionParamNames = ExtraScheduleParameterSchema.Fields[1].Names;
        public static readonly string[] NameParamNames = ExtraScheduleParameterSchema.Fields[2].Names;
        public static readonly string[] MarkParamNames = ExtraScheduleParameterSchema.Fields[3].Names;
        public static readonly string[] CodeParamNames = ExtraScheduleParameterSchema.Fields[4].Names;
        public static readonly string[] ManufacturerParamNames = ExtraScheduleParameterSchema.Fields[5].Names;
        public static readonly string[] UnitParamNames = ExtraScheduleParameterSchema.Fields[6].Names;
        public static readonly string[] CountParamNames = ExtraScheduleParameterSchema.Fields[7].Names;
        public static readonly string[] WeightParamNames = ExtraScheduleParameterSchema.Fields[8].Names;
        public static readonly string[] NoteParamNames = ExtraScheduleParameterSchema.Fields[9].Names;
        public static readonly Guid CodeParamGuid = ExtraScheduleParameterSchema.Fields[4].Guid;
        public static readonly Guid ManufacturerParamGuid = ExtraScheduleParameterSchema.Fields[5].Guid;
        private static readonly ExtraScheduleParameterSchema.Field[] AdskParams = ExtraScheduleParameterSchema.Fields;

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
                            if (string.IsNullOrWhiteSpace(item.Name) && elem.get_Parameter(AdskParams[2].Guid) == null)
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

            using (TransactionGroup syncGroup = new TransactionGroup(doc, "Допы в спецификацию"))
            {
                syncGroup.Start();
                var syncedIds = new Dictionary<ExtraScheduleItem, int>();

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

                            if (instance == null)
                                throw new InvalidOperationException($"Не удалось создать строку {index + 1}: {item.Name}.");

                            // Установка метки плагина в Comments
                            var commentParam = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (commentParam != null && !commentParam.IsReadOnly)
                            {
                                commentParam.Set(MarkerComment);
                            }

                            targetElem = instance;
                            created++;
                        }

                        // Запись параметров ADSK
                        ApplyParametersToElement(targetElem, item);
                        syncedIds[item] = targetElem.Id.IntegerValue;
                        index++;
                    }

                    // Новые экземпляры возвращают старое значение до регенерации документа.
                    doc.Regenerate();
                    foreach (var entry in syncedIds)
                    {
                        var element = doc.GetElement(new ElementId(entry.Value));
                        VerifyParameters(element, entry.Key);
                    }
                    if (t.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit отменил синхронизацию дополнительных элементов.");
                }
                if (syncGroup.Assimilate() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit отменил сохранение дополнительных элементов.");
                foreach (var entry in syncedIds)
                {
                    // Revit удаляет крайние пробельные символы у текстовых параметров.
                    // Обновляем строку только после успеха всей группы транзакций.
                    entry.Key.Group = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Group);
                    entry.Key.Position = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Position);
                    entry.Key.Name = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Name);
                    entry.Key.Mark = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Mark);
                    entry.Key.Code = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Code);
                    entry.Key.Manufacturer = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Manufacturer);
                    entry.Key.Unit = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Unit);
                    entry.Key.Note = ExtraScheduleParameterSchema.NormalizeText(entry.Key.Note);
                    entry.Key.Id = entry.Value;
                    entry.Key.IsNew = false;
                    entry.Key.IsModified = false;
                }
            }

            return (created, updated, deleted);
        }

        private static void ApplyParametersToElement(Element elem, ExtraScheduleItem item)
        {
            ExtraScheduleParameterSchema.Write(item, (field, value) =>
            {
                // Never use the first same-name parameter: schedules bind to the shared GUID.
                var parameter = elem.get_Parameter(field.Guid);
                var storage = field.IsNumber ? StorageType.Double : StorageType.String;
                if (parameter == null || parameter.IsReadOnly || parameter.StorageType != storage ||
                    parameter.Element?.Id != elem.Id)
                    throw new InvalidOperationException($"Не удалось записать {field.Name} у элемента {elem.Id}: " +
                        "общий параметр ADSK отсутствует или недоступен для записи в экземпляре.");

                // Set may report false for an unchanged value. The definitive check is after regeneration.
                if (field.IsNumber) parameter.Set((double)value);
                else parameter.Set((string)value);
                SetParam(elem, field.Names, value, setAllMatches: true);
            });
        }

        private static void VerifyParameters(Element elem, ExtraScheduleItem item)
        {
            try
            {
                ExtraScheduleParameterSchema.Verify(item, field =>
                {
                    var parameter = elem?.get_Parameter(field.Guid);
                    if (parameter == null || parameter.Element?.Id != elem.Id) return null;
                    if (field.IsNumber)
                        return parameter.StorageType == StorageType.Double ? (object)parameter.AsDouble() : null;
                    return parameter.StorageType == StorageType.String ? parameter.AsString() ?? "" : null;
                }, elem?.Id.IntegerValue);
            }
            catch (Exception ex)
            {
                Logger.Log($"Проверка ADSK: Revit {elem?.Document.Application.VersionNumber}, элемент {elem?.Id}: {ex.Message}", "ERROR");
                throw;
            }
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
                if (symId != null && doc.GetElement(symId) is FamilySymbol fs && fs.Category?.Id.IntegerValue == (int)bic)
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
                        var loadedSymbol = doc.GetElement(symbolId) as FamilySymbol;
                        if (loadedSymbol?.Category?.Id.IntegerValue == (int)bic) return loadedSymbol;
                    }
                }
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
            // An older release used the official unit GUID under a plural name.
            // Prefer GUID first to avoid adding a second parameter with the same GUID.
            FamilyParameter fp = famMgr.Parameters.Cast<FamilyParameter>()
                .FirstOrDefault(p => p.IsShared && p.GUID == paramGuid)
                ?? famMgr.get_Parameter(paramName);
            var canonical = grp?.Definitions?.get_Item(paramName) as ExternalDefinition;
            if (canonical == null)
                throw new InvalidOperationException($"Не найдено определение ADSK: {paramName}.");
            if (fp != null && (!fp.IsShared || fp.GUID != paramGuid))
            {
                // Preserve values while replacing old BCC/local GUIDs for every schedule field.
                string temporaryName = "BCC_Migrate_" + Guid.NewGuid().ToString("N");
                if (fp.IsShared)
                    fp = famMgr.ReplaceParameter(fp, temporaryName, GroupTypeId.Data, isInstance);
                else
                    famMgr.RenameParameter(fp, temporaryName);
                fp = famMgr.ReplaceParameter(fp, canonical, GroupTypeId.Data, isInstance);
                modified = true;
            }
            if (fp == null)
            {
                fp = famMgr.AddParameter(canonical, GroupTypeId.Data, isInstance);
                modified = true;
            }
            if (!fp.IsInstance && isInstance)
            {
                famMgr.MakeInstance(fp);
                modified = true;
            }
            if (!fp.IsShared || fp.GUID != paramGuid || fp.IsInstance != isInstance ||
                !fp.Definition.GetDataType().Equals(specTypeId))
                throw new InvalidOperationException($"Некорректное определение параметра {paramName} в семействе.");
            return fp;
        }

        private static void EnsureFamilyParameters(Autodesk.Revit.ApplicationServices.Application app, FamilyManager famMgr, ref bool modified)
        {
            if (famMgr == null) throw new InvalidOperationException("Недоступны параметры семейства BimboClub.");
            // Keep existing legacy schedule columns independent per row as well.
            foreach (var parameter in famMgr.Parameters.Cast<FamilyParameter>().ToList())
            {
                if (!parameter.IsInstance && AdskParams.Any(f => f.Names.Contains(parameter.Definition.Name)))
                {
                    famMgr.MakeInstance(parameter);
                    modified = true;
                }
            }
            string origShared = app.SharedParametersFilename;
            try
            {
                app.SharedParametersFilename = EnsureAdskSharedParamFile();
                DefinitionFile defFile = app.OpenSharedParameterFile();
                var defGroup = defFile?.Groups.get_Item("ADSK");
                if (defGroup == null) throw new InvalidOperationException("Не удалось открыть определения ADSK.");
                foreach (var field in AdskParams)
                    EnsureFamilyParameter(famMgr, defGroup, field.Name,
                        field.IsNumber ? SpecTypeId.Number : SpecTypeId.String.Text, field.Guid, ref modified);
            }
            finally
            {
                app.SharedParametersFilename = origShared;
            }
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

                // A loaded family must be repaired independently of the disk cache.
                try
                {
                    var family = new FilteredElementCollector(doc)
                        .OfClass(typeof(Family))
                        .Cast<Family>()
                        .FirstOrDefault(f => f != null && string.Equals(f.Name, famName, StringComparison.OrdinalIgnoreCase));

                    if (family == null)
                    {
                        EnsureFamilyFile(doc.Application, famName, bic);
                        continue;
                    }

                    if (family != null && family.IsEditable)
                    {
                        // Проверяем и загруженное семейство: исправление только RFA на диске
                        // не меняет параметры экземпляров, уже находящихся в проекте.
                        Document famDoc = doc.EditFamily(family);
                        if (famDoc != null)
                        {
                            try
                            {
                                bool modified = false;
                                using (Transaction tf = new Transaction(famDoc, "BimboClub: Параметры экземпляра"))
                                {
                                    tf.Start();
                                    EnsureFamilyParameters(doc.Application, famDoc.FamilyManager, ref modified);
                                    if (tf.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException($"Revit отменил настройку семейства {famName}.");
                                }

                                if (modified && famDoc.LoadFamily(doc, new ExtraScheduleFamilyLoadOptions()) == null)
                                    throw new InvalidOperationException($"Не удалось обновить семейство {famName} в проекте.");
                            }
                            finally
                            {
                                famDoc.Close(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Ошибка проверки параметров семейства {famName}: {ex.Message}", "WARN");
                    throw;
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
                Document existingFamDoc = null;
                try
                {
                    existingFamDoc = app.OpenDocumentFile(targetPath);
                    if (existingFamDoc != null)
                    {
                        bool modified = false;
                        using (Transaction tParams = new Transaction(existingFamDoc, "Проверка параметров экземпляра"))
                        {
                            tParams.Start();
                            EnsureFamilyParameters(app, existingFamDoc.FamilyManager, ref modified);
                            if (tParams.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException($"Revit отменил настройку семейства {famName}.");
                        }

                        if (modified)
                        {
                            existingFamDoc.Save();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Проверка существующего файла семейства {famName}: {ex.Message}", "DEBUG");
                    throw;
                }
                finally
                {
                    existingFamDoc?.Close(false);
                }
                return targetPath;
            }

            // Поиск шаблона Metric Generic Model.rft
            string templatePath = FindGenericModelTemplate(app.VersionNumber);
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                Document famDoc = null;
                try
                {
                    famDoc = app.NewFamilyDocument(templatePath);
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
                                    if (tCat.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException($"Revit отменил категорию семейства {famName}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Не удалось назначить категорию {bic} семейству {famName}: {ex.Message}", "WARN");
                                throw;
                            }
                        }

                        // Добавляем параметры ADSK как параметры ЭКЗЕМПЛЯРА
                        bool modified = false;
                        using (Transaction tParams = new Transaction(famDoc, "Параметры ADSK (экземпляр)"))
                        {
                            tParams.Start();
                            EnsureFamilyParameters(app, famDoc.FamilyManager, ref modified);
                            if (tParams.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException($"Revit отменил настройку семейства {famName}.");
                        }

                        SaveAsOptions opt = new SaveAsOptions { OverwriteExistingFile = true };
                        famDoc.SaveAs(targetPath, opt);
                        return targetPath;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка генерации семейства " + famName, ex);
                    throw;
                }
                finally
                {
                    famDoc?.Close(false);
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

        private static Parameter GetReadableParameter(Element elem, ExtraScheduleParameterSchema.Field field)
        {
            if (field == null) return null;
            if (field.LegacyGuid.HasValue && elem is FamilyInstance instance &&
                (instance.Symbol?.Family?.Name == FamilyName ||
                 instance.Symbol?.Family?.Name?.StartsWith("BCC_Доп", StringComparison.OrdinalIgnoreCase) == true))
            {
                // Only BCC families get the old-schema fallback, and only while the
                // obsolete GUID is still part of that family (not a project binding).
                var legacy = elem.get_Parameter(field.LegacyGuid.Value) ?? instance.Symbol.get_Parameter(field.LegacyGuid.Value);
                if (legacy != null && !elem.Document.ParameterBindings.Contains(legacy.Definition)) return legacy;
            }
            return elem.get_Parameter(field.Guid);
        }

        public static string GetStringParam(Element elem, string[] paramNames, string defaultValue = "")
        {
            if (elem == null) return defaultValue;

            var field = ExtraScheduleParameterSchema.Find(paramNames);
            Guid preferredGuid = field?.Guid ?? Guid.Empty;
            var preferred = GetReadableParameter(elem, field);
            // An explicitly cleared canonical value must not resurrect a stale alias/type value.
            if (preferred?.StorageType == StorageType.String)
                return (preferred.AsString() ?? "").Trim();

            bool hasInstanceParameter = false;
            foreach (string name in paramNames)
            {
                foreach (Parameter p in elem.GetParameters(name))
                {
                    hasInstanceParameter = true;
                    if (p.StorageType == StorageType.String && p.HasValue)
                    {
                        string val = p.AsString();
                        if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                    }
                }
            }

            // Старые семейства могли хранить поля спецификации в типе.
            if (!hasInstanceParameter && preferredGuid != Guid.Empty && elem is FamilyInstance instance)
                return GetStringParam(instance.Symbol, paramNames, defaultValue);

            return defaultValue;
        }

        public static double GetDoubleParam(Element elem, string[] paramNames, double defaultValue = 0.0)
        {
            if (elem == null) return defaultValue;

            var field = ExtraScheduleParameterSchema.Find(paramNames);
            var preferred = GetReadableParameter(elem, field);
            if (preferred?.StorageType == StorageType.Double) return preferred.AsDouble();

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
                foreach (Parameter p in elem.GetParameters(name))
                {
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
            }

            return anySet;
        }

        #endregion
    }
}
