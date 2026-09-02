using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub
{
    public class FamilyEntryItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public Family RawFamily { get; set; }
        public string Name => RawFamily?.Name ?? "";
        public string CategoryName { get; set; } = "Без категории";
        public ElementId Id => RawFamily?.Id ?? ElementId.InvalidElementId;
        public int SymbolsCount { get; set; } = 0;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public HashSet<string> ParameterNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class FamilyParamItem : INotifyPropertyChanged
    {
        private bool _isSelected = false;

        public string Name { get; set; }
        public string Guid { get; set; } = "";
        public bool IsShared { get; set; }
        public bool IsInstance { get; set; }
        public int FamiliesCount { get; set; }

        public string ParamTypeDisplay => IsShared ? "Общий" : "Семейный";
        public string BindingDisplay => IsInstance ? "Экземпляр" : "Тип";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class CustomFamilyLoadOptions : IFamilyLoadOptions
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

    public class DeletionResult
    {
        public int ProcessedFamilies { get; set; }
        public int ModifiedFamilies { get; set; }
        public int DeletedParametersCount { get; set; }
        public List<string> SuccessLogs { get; set; } = new List<string>();
        public List<string> ErrorLogs { get; set; } = new List<string>();
    }

    public static class FamilyParameterService
    {
        public static List<FamilyEntryItem> GetProjectEditableFamilies(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            List<Family> families = collector
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.IsEditable && !f.IsInPlace)
                .OrderBy(f => f.Name)
                .ToList();

            List<FamilyEntryItem> result = new List<FamilyEntryItem>();

            foreach (Family fam in families)
            {
                string catName = "Без категории";
                try
                {
                    if (fam.FamilyCategory != null)
                    {
                        catName = fam.FamilyCategory.Name;
                    }
                    else
                    {
                        var symIds = fam.GetFamilySymbolIds();
                        if (symIds.Count > 0)
                        {
                            Element sym = doc.GetElement(symIds.First());
                            if (sym?.Category != null)
                            {
                                catName = sym.Category.Name;
                            }
                        }
                    }
                }
                catch { }

                int symbolsCount = fam.GetFamilySymbolIds()?.Count ?? 0;

                result.Add(new FamilyEntryItem
                {
                    RawFamily = fam,
                    CategoryName = catName,
                    SymbolsCount = symbolsCount,
                    IsSelected = true
                });
            }

            return result;
        }

        /// <summary>
        /// Быстрое сканирование параметров семейств проекта через символы и FamilyManager
        /// </summary>
        public static List<FamilyParamItem> ScanParameters(
            Document doc,
            List<FamilyEntryItem> families,
            Action<int, int, string> progressCallback = null)
        {
            Dictionary<string, FamilyParamItem> paramDict = new Dictionary<string, FamilyParamItem>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<ElementId>> paramFamilies = new Dictionary<string, HashSet<ElementId>>(StringComparer.OrdinalIgnoreCase);

            int total = families.Count;
            int current = 0;

            foreach (FamilyEntryItem famItem in families)
            {
                current++;
                progressCallback?.Invoke(current, total, $"Сканирование семейства: {famItem.Name} ({current}/{total})");

                Family fam = famItem.RawFamily;
                if (fam == null || !fam.IsValidObject) continue;

                // 1. Быстрый сбор параметров типов через символы в проекте
                var symbolIds = fam.GetFamilySymbolIds();
                if (symbolIds != null && symbolIds.Count > 0)
                {
                    foreach (ElementId symId in symbolIds)
                    {
                        if (!(doc.GetElement(symId) is FamilySymbol symbol)) continue;

                        foreach (Parameter p in symbol.Parameters)
                        {
                            if (p == null || p.Definition == null) continue;
                            string pName = p.Definition.Name;
                            if (string.IsNullOrWhiteSpace(pName)) continue;

                            famItem.ParameterNames.Add(pName);

                            if (!paramFamilies.ContainsKey(pName))
                            {
                                paramFamilies[pName] = new HashSet<ElementId>();
                            }
                            paramFamilies[pName].Add(fam.Id);

                            if (!paramDict.ContainsKey(pName))
                            {
                                paramDict[pName] = new FamilyParamItem
                                {
                                    Name = pName,
                                    IsShared = p.IsShared,
                                    Guid = p.IsShared && p.GUID != Guid.Empty ? p.GUID.ToString() : "",
                                    IsInstance = false,
                                    FamiliesCount = 0
                                };
                            }
                        }
                    }
                }

                // 2. Открытие семейного документа в памяти для полного анализа (включая параметры экземпляра)
                try
                {
                    Document famDoc = doc.EditFamily(fam);
                    if (famDoc != null)
                    {
                        FamilyManager famMgr = famDoc.FamilyManager;
                        if (famMgr != null)
                        {
                            foreach (FamilyParameter fp in famMgr.Parameters)
                            {
                                if (fp == null || fp.Definition == null) continue;
                                string pName = fp.Definition.Name;
                                if (string.IsNullOrWhiteSpace(pName)) continue;

                                famItem.ParameterNames.Add(pName);

                                if (!paramFamilies.ContainsKey(pName))
                                {
                                    paramFamilies[pName] = new HashSet<ElementId>();
                                }
                                paramFamilies[pName].Add(fam.Id);

                                if (!paramDict.ContainsKey(pName))
                                {
                                    paramDict[pName] = new FamilyParamItem
                                    {
                                        Name = pName,
                                        IsShared = fp.IsShared,
                                        Guid = fp.IsShared && fp.GUID != Guid.Empty ? fp.GUID.ToString() : "",
                                        IsInstance = fp.IsInstance,
                                        FamiliesCount = 0
                                    };
                                }
                                else
                                {
                                    if (fp.IsInstance) paramDict[pName].IsInstance = true;
                                    if (fp.IsShared)
                                    {
                                        paramDict[pName].IsShared = true;
                                        if (fp.GUID != Guid.Empty) paramDict[pName].Guid = fp.GUID.ToString();
                                    }
                                }
                            }
                        }
                        famDoc.Close(false);
                    }
                }
                catch
                {
                    // Если семейство защищено или не может быть открыто в памяти, пропускаем
                }
            }

            foreach (var kvp in paramDict)
            {
                if (paramFamilies.TryGetValue(kvp.Key, out var set))
                {
                    kvp.Value.FamiliesCount = set.Count;
                }
            }

            return paramDict.Values.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// Пакетное удаление параметров из выбранных семейств с перезагрузкой
        /// </summary>
        public static DeletionResult DeleteParameters(
            Document doc,
            List<FamilyEntryItem> targetFamilies,
            List<string> parameterNamesToDelete,
            Action<int, int, string> progressCallback = null)
        {
            DeletionResult result = new DeletionResult();
            if (targetFamilies == null || targetFamilies.Count == 0 || parameterNamesToDelete == null || parameterNamesToDelete.Count == 0)
            {
                return result;
            }

            HashSet<string> deleteSet = new HashSet<string>(parameterNamesToDelete, StringComparer.OrdinalIgnoreCase);
            CustomFamilyLoadOptions loadOptions = new CustomFamilyLoadOptions();

            int total = targetFamilies.Count;
            int current = 0;

            foreach (FamilyEntryItem famItem in targetFamilies)
            {
                current++;
                Family fam = famItem.RawFamily;
                if (fam == null || !fam.IsValidObject || !fam.IsEditable)
                {
                    result.ErrorLogs.Add($"Семейство \"{famItem.Name}\" не является редактируемым (пропущено).");
                    continue;
                }

                progressCallback?.Invoke(current, total, $"Обработка семейства \"{fam.Name}\" ({current}/{total})...");

                Document famDoc = null;
                try
                {
                    famDoc = doc.EditFamily(fam);
                    if (famDoc == null)
                    {
                        result.ErrorLogs.Add($"Не удалось открыть редактор для семейства \"{fam.Name}\".");
                        continue;
                    }

                    FamilyManager famMgr = famDoc.FamilyManager;
                    if (famMgr == null)
                    {
                        famDoc.Close(false);
                        continue;
                    }

                    List<FamilyParameter> toRemove = new List<FamilyParameter>();
                    foreach (FamilyParameter fp in famMgr.Parameters)
                    {
                        if (fp == null || fp.Definition == null) continue;
                        if (deleteSet.Contains(fp.Definition.Name))
                        {
                            toRemove.Add(fp);
                        }
                    }

                    if (toRemove.Count > 0)
                    {
                        int deletedInFamily = 0;
                        using (Transaction t = new Transaction(famDoc, "Удаление параметров семейства"))
                        {
                            t.Start();
                            foreach (FamilyParameter p in toRemove)
                            {
                                try
                                {
                                    famMgr.RemoveParameter(p);
                                    deletedInFamily++;
                                }
                                catch (Exception ex)
                                {
                                    result.ErrorLogs.Add($"Семейство \"{fam.Name}\": ошибка удаления параметра \"{p.Definition.Name}\" ({ex.Message}).");
                                }
                            }
                            t.Commit();
                        }

                        if (deletedInFamily > 0)
                        {
                            famDoc.LoadFamily(doc, loadOptions);
                            result.ModifiedFamilies++;
                            result.DeletedParametersCount += deletedInFamily;
                            result.SuccessLogs.Add($"Семейство \"{fam.Name}\": удалено параметров — {deletedInFamily}.");
                        }
                    }

                    result.ProcessedFamilies++;
                }
                catch (Exception ex)
                {
                    result.ErrorLogs.Add($"Семейство \"{fam.Name}\": критическая ошибка при обработке — {ex.Message}");
                }
                finally
                {
                    if (famDoc != null && famDoc.IsValidObject)
                    {
                        try
                        {
                            famDoc.Close(false);
                        }
                        catch { }
                    }
                }
            }

            return result;
        }
    }
}
