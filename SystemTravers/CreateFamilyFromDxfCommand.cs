using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateFamilyFromDxfCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Нет активного документа Revit.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;

            try
            {
                // 1. Открытие диалогового окна выбора параметров создания семейства
                CreateFamilyFromDxfWindow window = new CreateFamilyFromDxfWindow();
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                string dxfPath = window.SelectedDxfPath;
                string familyName = window.SelectedFamilyName;
                BuiltInCategory targetCategoryType = window.SelectedCategory;
                bool autoPlace = window.AutoPlace;

                if (string.IsNullOrEmpty(dxfPath) || !File.Exists(dxfPath))
                {
                    return Result.Cancelled;
                }

                // 2. Поиск подходящего шаблона семейства
                string templatePath = FindTemplateForCategory(app, targetCategoryType);
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    TaskDialog.Show("Семейство по DXF", "Не найден шаблон семейства. Операция отменена.");
                    return Result.Cancelled;
                }
                Logger.Log($"Используется шаблон семейства: {templatePath}", "INFO");

                string dxfDir = Path.GetDirectoryName(dxfPath);
                string rfaPath = Path.Combine(dxfDir, familyName + ".rfa");

                // 3. Создание документа семейства
                Document famDoc = app.NewFamilyDocument(templatePath);
                if (famDoc == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось создать документ семейства на основе шаблона: " + templatePath);
                    return Result.Failed;
                }

                // 4. Назначение выбранной категории семейства (если текущая отличается от целевой)
                if (famDoc.OwnerFamily != null)
                {
                    try
                    {
                        ElementId currentCatId = famDoc.OwnerFamily.FamilyCategoryId;
                        ElementId targetCatId = new ElementId(targetCategoryType);

                        if (currentCatId == null || currentCatId.IntegerValue != targetCatId.IntegerValue)
                        {
                            using (Transaction tCat = new Transaction(famDoc, "Назначение категории"))
                            {
                                tCat.Start();
                                bool applied = false;

                                try
                                {
                                    famDoc.OwnerFamily.FamilyCategoryId = targetCatId;
                                    applied = true;
                                    Logger.Log($"Категория семейства успешно изменена на {targetCategoryType} через FamilyCategoryId", "INFO");
                                }
                                catch (Exception exId)
                                {
                                    Logger.Log($"Warning: не удалось назначить FamilyCategoryId {targetCategoryType}: {exId.Message}", "WARN");
                                }

                                if (!applied)
                                {
                                    try
                                    {
                                        Category targetCategory = Category.GetCategory(famDoc, targetCategoryType)
                                                               ?? famDoc.Settings.Categories.get_Item(targetCategoryType);
                                        if (targetCategory != null)
                                        {
                                            famDoc.OwnerFamily.FamilyCategory = targetCategory;
                                            Logger.Log($"Категория семейства успешно изменена на {targetCategoryType} через FamilyCategory", "INFO");
                                        }
                                        else
                                        {
                                            Logger.Log($"Warning: не удалось найти категорию {targetCategoryType} в famDoc", "WARN");
                                        }
                                    }
                                    catch (Exception exCat)
                                    {
                                        Logger.Log($"Warning: не удалось назначить FamilyCategory {targetCategoryType}: {exCat.Message}", "WARN");
                                    }
                                }

                                tCat.Commit();
                            }
                        }
                        else
                        {
                            Logger.Log($"Семейство уже имеет целевую категорию: {famDoc.OwnerFamily.FamilyCategory?.Name}", "INFO");
                        }
                    }
                    catch (Exception exCatTrans)
                    {
                        Logger.Log($"Error: ошибка транзакции назначения категории: {exCatTrans.Message}", "ERROR");
                    }
                }

                // 5. Импорт DXF в документ семейства
                View targetView = famDoc.ActiveView;
                if (targetView == null || targetView.IsTemplate)
                {
                    targetView = new FilteredElementCollector(famDoc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .FirstOrDefault(v => !v.IsTemplate)
                        ?? (View)new FilteredElementCollector(famDoc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .FirstOrDefault(v => !v.IsTemplate);
                }

                DWGImportOptions importOptions = new DWGImportOptions
                {
                    Placement = ImportPlacement.Origin,
                    ColorMode = ImportColorMode.Preserved,
                    Unit = ImportUnit.Default
                };

                using (Transaction tImport = new Transaction(famDoc, "Импорт DXF геометрии"))
                {
                    tImport.Start();
                    ElementId importId;
                    bool imported = famDoc.Import(dxfPath, importOptions, targetView, out importId);
                    if (!imported || importId == ElementId.InvalidElementId)
                    {
                        tImport.RollBack();
                        famDoc.Close(false);
                        TaskDialog.Show("Ошибка импорта", "Revit не смог импортировать файл: " + dxfPath);
                        return Result.Failed;
                    }
                    tImport.Commit();
                }

                // 6. Сохранение файла .rfa
                try
                {
                    famDoc.SaveAs(rfaPath, new SaveAsOptions { OverwriteExistingFile = true });
                }
                catch (Exception exSave)
                {
                    Logger.Log($"Не удалось сохранить RFA в папку исходника: {exSave.Message}. Сохраняю во временную папку.", "WARN");
                    rfaPath = Path.Combine(Path.GetTempPath(), familyName + "_" + Guid.NewGuid().ToString().Substring(0, 6) + ".rfa");
                    famDoc.SaveAs(rfaPath, new SaveAsOptions { OverwriteExistingFile = true });
                }

                // 7. Загрузка в текущий проект
                Family family = famDoc.LoadFamily(doc, new BimboFamilyLoadOption());
                famDoc.Close(false);

                if (family == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось загрузить созданное семейство в проект.");
                    return Result.Failed;
                }

                // 8. Поиск и активация типоразмера
                FamilySymbol symbol = null;
                ISet<ElementId> symbolIds = family.GetFamilySymbolIds();
                if (symbolIds != null && symbolIds.Count > 0)
                {
                    symbol = doc.GetElement(symbolIds.First()) as FamilySymbol;
                }

                if (symbol == null)
                {
                    TaskDialog.Show("Семейство по DXF", $"Семейство '{familyName}' успешно создано и загружено в проект.");
                    return Result.Succeeded;
                }

                using (Transaction tSymbol = new Transaction(doc, "Активация типоразмера"))
                {
                    tSymbol.Start();
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                    }
                    tSymbol.Commit();
                }

                // 9. Размещение на виде (если выбран чекбокс)
                if (autoPlace)
                {
                    try
                    {
                        uidoc.PromptForFamilyInstancePlacement(symbol);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // Пользователь закончил размещение или нажал ESC
                    }
                }
                else
                {
                    TaskDialog.Show("Семейство по DXF", $"Семейство '{familyName}' успешно создано и загружено в проект.");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Семейство по DXF", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static string FindTemplateForCategory(Autodesk.Revit.ApplicationServices.Application app, BuiltInCategory targetCategory)
        {
            // 1. Проверяем путь к шаблонам, настроенный в Revit
            string basePath = app.FamilyTemplatePath;
            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                string found = SearchTemplateInDirectory(basePath, targetCategory);
                if (!string.IsNullOrEmpty(found)) return found;
            }

            // 2. Стандартные системные каталоги Revit Family Templates
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string year = app.VersionNumber;

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(progData, "Autodesk", $"RVT {year}", "Family Templates"),
                Path.Combine(progData, "Autodesk", $"Revit {year}", "Family Templates"),
                Path.Combine(progData, "Autodesk", "RVT " + year, "Family Templates", "Russian"),
                Path.Combine(progData, "Autodesk", "RVT " + year, "Family Templates", "English"),
                @"C:\ProgramData\Autodesk\RVT 2026\Family Templates",
                @"C:\ProgramData\Autodesk\RVT 2025\Family Templates",
                @"C:\ProgramData\Autodesk\RVT 2024\Family Templates",
                @"C:\ProgramData\Autodesk\RVT 2023\Family Templates",
                @"C:\ProgramData\Autodesk\RVT 2022\Family Templates",
                @"C:\ProgramData\Autodesk\RVT 2021\Family Templates"
            };

            foreach (string path in candidatePaths)
            {
                if (Directory.Exists(path))
                {
                    string found = SearchTemplateInDirectory(path, targetCategory);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }

            // 3. Fallback: ручной выбор пользователем
            OpenFileDialog rftDialog = new OpenFileDialog
            {
                Title = "Укажите шаблон семейства (Метрическая система, типовая модель.rft)",
                Filter = "Шаблоны семейств (*.rft)|*.rft|Все файлы (*.*)|*.*"
            };

            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                rftDialog.InitialDirectory = basePath;
            }

            if (rftDialog.ShowDialog() == true)
            {
                return rftDialog.FileName;
            }

            return null;
        }

        private static string SearchTemplateInDirectory(string dir, BuiltInCategory targetCategory)
        {
            try
            {
                // Сначала ищем прямой шаблон для выбранной категории (если применимо)
                string[] specificPatterns = GetSpecificTemplatePatterns(targetCategory);
                if (specificPatterns != null && specificPatterns.Length > 0)
                {
                    foreach (string pat in specificPatterns)
                    {
                        string[] files = Directory.GetFiles(dir, pat, SearchOption.AllDirectories);
                        string match = files.FirstOrDefault(f => IsValidTemplate(f));
                        if (match != null)
                        {
                            Logger.Log($"Найден специализированный шаблон для категории {targetCategory}: {match}", "INFO");
                            return match;
                        }
                    }
                }

                // Ищем стандартный шаблон типовой модели
                string[] genericPatterns = new[]
                {
                    "*типовая модель*.rft",
                    "*Generic Model*.rft",
                    "*GenericModel*.rft"
                };

                List<string> candidateFiles = new List<string>();
                foreach (string pat in genericPatterns)
                {
                    string[] files = Directory.GetFiles(dir, pat, SearchOption.AllDirectories);
                    candidateFiles.AddRange(files);
                }

                if (candidateFiles.Count > 0)
                {
                    // 1. Точное совпадение со стандартным шаблоном (Метрическая система, типовая модель.rft)
                    string exact = candidateFiles.FirstOrDefault(f =>
                    {
                        string fn = Path.GetFileNameWithoutExtension(f).Trim().ToLowerInvariant();
                        return fn == "метрическая система, типовая модель" ||
                               fn == "metric generic model" ||
                               fn == "типовая модель" ||
                               fn == "generic model";
                    });
                    if (exact != null)
                    {
                        Logger.Log($"Найден точный шаблон типовой модели: {exact}", "INFO");
                        return exact;
                    }

                    // 2. Любой не-адаптивный и не-хостовый шаблон типовой модели
                    string cleanMatch = candidateFiles.FirstOrDefault(f => IsValidTemplate(f));
                    if (cleanMatch != null)
                    {
                        Logger.Log($"Найден подходящий шаблон типовой модели: {cleanMatch}", "INFO");
                        return cleanMatch;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"SearchTemplateInDirectory error in {dir}: {ex.Message}", "WARN");
            }

            return null;
        }

        private static bool IsValidTemplate(string filePath)
        {
            string fn = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

            // КРИТИЧЕСКИ ВАЖНО: исключаем адаптивные шаблоны! В адаптивных семействах Revit запрещает смену категории
            if (fn.Contains("адаптивн") || fn.Contains("adaptive")) return false;

            // Исключаем шаблоны на основе образца
            if (fn.Contains("образц") || fn.Contains("pattern")) return false;

            // Исключаем хостовые шаблоны (на основе стены, грани, потолка, пола, линии, крыши и т.д.)
            if (fn.Contains("на основе") || fn.Contains("based")) return false;
            if (fn.Contains("настенн") || fn.Contains("потолочн") || fn.Contains("подрезк")) return false;
            if (fn.Contains("стена") || fn.Contains("wall")) return false;
            if (fn.Contains("потолок") || fn.Contains("ceiling")) return false;
            if (fn.Contains("пол") || fn.Contains("floor")) return false;
            if (fn.Contains("крыш") || fn.Contains("roof")) return false;
            if (fn.Contains("линия") || fn.Contains("line")) return false;
            if (fn.Contains("грань") || fn.Contains("face")) return false;
            if (fn.Contains("двух уровн") || fn.Contains("two level")) return false;

            return true;
        }

        private static string[] GetSpecificTemplatePatterns(BuiltInCategory cat)
        {
            switch (cat)
            {
                case BuiltInCategory.OST_MechanicalEquipment:
                    return new[] { "*оборудование*.rft", "*Mechanical Equipment*.rft" };
                case BuiltInCategory.OST_ElectricalEquipment:
                    return new[] { "*электрооборудование*.rft", "*Electrical Equipment*.rft" };
                case BuiltInCategory.OST_PlumbingFixtures:
                    return new[] { "*сантехнический прибор*.rft", "*Plumbing Fixture*.rft" };
                case BuiltInCategory.OST_LightingFixtures:
                    return new[] { "*осветительный прибор*.rft", "*Lighting Fixture*.rft" };
                case BuiltInCategory.OST_Furniture:
                    return new[] { "*мебель*.rft", "*Furniture*.rft" };
                case BuiltInCategory.OST_SpecialityEquipment:
                    return new[] { "*специальное оборудование*.rft", "*Specialty Equipment*.rft" };
                default:
                    return null;
            }
        }

        private class BimboFamilyLoadOption : IFamilyLoadOptions
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
    }
}
