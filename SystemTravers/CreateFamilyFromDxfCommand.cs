using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using DialogResult = System.Windows.Forms.DialogResult;

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
                // 1. Выбор конкретного файла DXF
                string dxfPath = null;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Выберите файл DXF для создания семейства";
                    ofd.Filter = "AutoCAD DXF (*.dxf)|*.dxf|Все файлы (*.*)|*.*";
                    ofd.Multiselect = false;
                    ofd.CheckFileExists = true;

                    if (ofd.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    dxfPath = ofd.FileName;
                }

                if (string.IsNullOrEmpty(dxfPath) || !File.Exists(dxfPath))
                {
                    return Result.Cancelled;
                }

                // 2. Поиск стандартного шаблона семейства "Метрическая система, типовая модель.rft"
                string templatePath = FindGenericModelTemplate(app);
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    TaskDialog.Show("Семейство по DXF", "Не найден шаблон семейства 'Метрическая система, типовая модель.rft'. Операция отменена.");
                    return Result.Cancelled;
                }

                string familyName = Path.GetFileNameWithoutExtension(dxfPath);
                string dxfDir = Path.GetDirectoryName(dxfPath);
                string rfaPath = Path.Combine(dxfDir, familyName + ".rfa");

                // 3. Создание документа семейства
                Document famDoc = app.NewFamilyDocument(templatePath);
                if (famDoc == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось создать документ семейства на основе шаблона: " + templatePath);
                    return Result.Failed;
                }

                // 4. Импорт DXF в документ семейства
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

                // 5. Сохранение файла .rfa
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

                // 6. Загрузка в текущий проект
                Family family = famDoc.LoadFamily(doc, new BimboFamilyLoadOption());
                famDoc.Close(false);

                if (family == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось загрузить созданное семейство в проект.");
                    return Result.Failed;
                }

                // 7. Поиск и активация типоразмера
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

                // 8. Загрузить и сразу активировать размещение на виде
                try
                {
                    uidoc.PromptForFamilyInstancePlacement(symbol);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // Пользователь закончил размещение или нажал ESC
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

        private static string FindGenericModelTemplate(Autodesk.Revit.ApplicationServices.Application app)
        {
            // 1. Проверяем путь к шаблонам, настроенный в Revit
            string basePath = app.FamilyTemplatePath;
            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                string found = SearchTemplateInDirectory(basePath);
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
                    string found = SearchTemplateInDirectory(path);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }

            // 3. Fallback: ручной выбор пользователем
            using (OpenFileDialog rftDialog = new OpenFileDialog())
            {
                rftDialog.Title = "Укажите шаблон семейства (Метрическая система, типовая модель.rft)";
                rftDialog.Filter = "Шаблоны семейств (*.rft)|*.rft|Все файлы (*.*)|*.*";
                if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                {
                    rftDialog.InitialDirectory = basePath;
                }

                if (rftDialog.ShowDialog() == DialogResult.OK)
                {
                    return rftDialog.FileName;
                }
            }

            return null;
        }

        private static string SearchTemplateInDirectory(string dir)
        {
            try
            {
                string[] patterns = new[]
                {
                    "*типовая модель*.rft",
                    "*Generic Model*.rft",
                    "*GenericModel*.rft"
                };

                foreach (string pat in patterns)
                {
                    string[] files = Directory.GetFiles(dir, pat, SearchOption.AllDirectories);
                    if (files.Length == 0) continue;

                    // Предпочитаем стандартный базовый шаблон (без привязок к стене, потолку, полу, грани)
                    string cleanMatch = files.FirstOrDefault(f =>
                    {
                        string fn = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        return !fn.Contains("на основе") && 
                               !fn.Contains("based") && 
                               !fn.Contains("стена") && 
                               !fn.Contains("wall") && 
                               !fn.Contains("потолок") && 
                               !fn.Contains("ceiling") && 
                               !fn.Contains("пол") && 
                               !fn.Contains("floor") && 
                               !fn.Contains("линия") && 
                               !fn.Contains("line") && 
                               !fn.Contains("грань") && 
                               !fn.Contains("face");
                    });

                    if (cleanMatch != null) return cleanMatch;
                    return files[0];
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"SearchTemplateInDirectory error in {dir}: {ex.Message}", "WARN");
            }

            return null;
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
