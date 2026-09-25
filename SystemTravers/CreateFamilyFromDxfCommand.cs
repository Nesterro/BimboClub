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
                bool convertToNative = window.ConvertToNativeGeometry;
                double extrusionHeightMm = window.ExtrusionHeightMm;

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

                ElementId importId = ElementId.InvalidElementId;
                using (Transaction tImport = new Transaction(famDoc, "Импорт DXF геометрии"))
                {
                    tImport.Start();
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

                // 5.1. Преобразование DXF в нативную 3D-геометрию Revit (FreeFormElement)
                int nativeFormsCount = 0;
                if (convertToNative && importId != ElementId.InvalidElementId)
                {
                    try
                    {
                        using (Transaction tConvert = new Transaction(famDoc, "Создание нативных 3D-тел Revit"))
                        {
                            tConvert.Start();
                            ImportInstance importInstance = famDoc.GetElement(importId) as ImportInstance;
                            if (importInstance != null)
                            {
                                nativeFormsCount = ConvertImportInstanceToNativeGeometry(famDoc, importInstance, extrusionHeightMm);
                                if (nativeFormsCount > 0)
                                {
                                    famDoc.Delete(importId);
                                    Logger.Log($"Успешно создано {nativeFormsCount} нативных 3D-тел Revit (FreeFormElement). Исходная CAD-подложка удалена.", "INFO");
                                }
                                else
                                {
                                    Logger.Log("3D-тел в DXF не обнаружено. CAD-подложка сохранена в семействе.", "WARN");
                                }
                            }
                            tConvert.Commit();
                        }
                    }
                    catch (Exception exConvert)
                    {
                        Logger.Log($"Ошибка при преобразовании в нативные 3D-тела: {exConvert.Message}", "WARN");
                    }
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

                string infoMsg = nativeFormsCount > 0
                    ? $"Семейство '{familyName}' успешно создано и загружено в проект!\n\n" +
                      $"• Создано нативных 3D-тел Revit (FreeFormElement): {nativeFormsCount}\n" +
                      $"• Исходная CAD-подложка: удалена (чистое семейство)\n" +
                      $"• Категория: {targetCategoryType}"
                    : $"Семейство '{familyName}' успешно создано и загружено в проект.\n\n" +
                      $"• Режим: CAD-импорт\n" +
                      $"• Категория: {targetCategoryType}";

                if (symbol == null)
                {
                    TaskDialog.Show("Семейство по DXF", infoMsg);
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
                    TaskDialog.Show("Семейство по DXF", infoMsg);
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

        private static int ConvertImportInstanceToNativeGeometry(Document famDoc, ImportInstance importInstance, double extrusionHeightMm)
        {
            int createdCount = 0;
            try
            {
                Options opt = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false,
                    DetailLevel = ViewDetailLevel.Fine
                };

                GeometryElement geomElement = importInstance.get_Geometry(opt);
                if (geomElement == null) return 0;

                List<Solid> solids = new List<Solid>();
                List<Mesh> meshes = new List<Mesh>();
                List<Curve> curves = new List<Curve>();

                ExtractGeometryRecursive(geomElement, solids, meshes, curves, Transform.Identity);

                Logger.Log($"Извлечено из DXF: тел Solid={solids.Count}, полигональных сеток Mesh={meshes.Count}, линий/кривых Curve={curves.Count}", "INFO");

                // 1. Создаем FreeFormElement для каждого найденного Solid
                foreach (Solid s in solids)
                {
                    if (s != null && s.Volume > 1e-7 && s.Faces.Size > 0)
                    {
                        try
                        {
                            FreeFormElement ffe = FreeFormElement.Create(famDoc, s);
                            if (ffe != null) createdCount++;
                        }
                        catch (Exception exFfe)
                        {
                            Logger.Log($"Warning: не удалось создать FreeFormElement из Solid: {exFfe.Message}", "WARN");
                        }
                    }
                }

                // 2. Если тел Solid нет или мало, а есть полигональные сетки Mesh - пробуем сшить их в Solid
                if (meshes.Count > 0)
                {
                    List<Solid> meshSolids = BuildSolidsFromMeshes(meshes);
                    foreach (Solid ms in meshSolids)
                    {
                        try
                        {
                            FreeFormElement ffe = FreeFormElement.Create(famDoc, ms);
                            if (ffe != null) createdCount++;
                        }
                        catch (Exception exMeshFfe)
                        {
                            Logger.Log($"Warning: не удалось создать FreeFormElement из сетки Mesh: {exMeshFfe.Message}", "WARN");
                        }
                    }
                }

                // 3. Если 3D-тел вообще нет, но есть замкнутые 2D контуры - выдавливаем их
                if (createdCount == 0 && curves.Count >= 3)
                {
                    double heightFeet = extrusionHeightMm / 304.8;
                    List<Solid> extrudedSolids = BuildSolidsFrom2DContours(curves, heightFeet);
                    foreach (Solid es in extrudedSolids)
                    {
                        try
                        {
                            FreeFormElement ffe = FreeFormElement.Create(famDoc, es);
                            if (ffe != null) createdCount++;
                        }
                        catch (Exception exExt)
                        {
                            Logger.Log($"Warning: не удалось создать FreeFormElement из 2D выдавливания: {exExt.Message}", "WARN");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка ConvertImportInstanceToNativeGeometry: {ex.Message}", "ERROR");
            }

            return createdCount;
        }

        private static void ExtractGeometryRecursive(
            GeometryElement geomElem,
            List<Solid> solids,
            List<Mesh> meshes,
            List<Curve> curves,
            Transform parentTransform)
        {
            if (geomElem == null) return;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid)
                {
                    if (solid.Volume > 1e-7 && solid.Faces.Size > 0)
                    {
                        if (!parentTransform.IsIdentity)
                        {
                            try
                            {
                                Solid transSolid = SolidUtils.CreateTransformed(solid, parentTransform);
                                solids.Add(transSolid);
                            }
                            catch
                            {
                                solids.Add(solid);
                            }
                        }
                        else
                        {
                            solids.Add(solid);
                        }
                    }
                }
                else if (obj is Mesh mesh)
                {
                    if (mesh.NumTriangles > 0)
                    {
                        if (!parentTransform.IsIdentity)
                        {
                            try
                            {
                                Mesh transMesh = mesh.get_Transformed(parentTransform);
                                meshes.Add(transMesh);
                            }
                            catch
                            {
                                meshes.Add(mesh);
                            }
                        }
                        else
                        {
                            meshes.Add(mesh);
                        }
                    }
                }
                else if (obj is Curve curve)
                {
                    curves.Add(!parentTransform.IsIdentity ? curve.CreateTransformed(parentTransform) : curve);
                }
                else if (obj is GeometryInstance inst)
                {
                    Transform combined = parentTransform.Multiply(inst.Transform);
                    GeometryElement instGeom = inst.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        ExtractGeometryRecursive(instGeom, solids, meshes, curves, Transform.Identity);
                    }
                    else
                    {
                        GeometryElement symGeom = inst.GetSymbolGeometry();
                        if (symGeom != null)
                        {
                            ExtractGeometryRecursive(symGeom, solids, meshes, curves, combined);
                        }
                    }
                }
            }
        }

        private static List<Solid> BuildSolidsFromMeshes(List<Mesh> meshes)
        {
            var result = new List<Solid>();
            if (meshes == null || meshes.Count == 0) return result;

            try
            {
                var builder = new TessellatedShapeBuilder();
                builder.OpenConnectedFaceSet(true);

                int validTriangles = 0;
                foreach (var mesh in meshes)
                {
                    if (mesh == null) continue;
                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        MeshTriangle tri = mesh.get_Triangle(i);
                        XYZ p0 = tri.get_Vertex(0);
                        XYZ p1 = tri.get_Vertex(1);
                        XYZ p2 = tri.get_Vertex(2);

                        if (p0.DistanceTo(p1) > 1e-4 && p1.DistanceTo(p2) > 1e-4 && p2.DistanceTo(p0) > 1e-4)
                        {
                            builder.AddFace(new TessellatedFace(new List<XYZ> { p0, p1, p2 }, ElementId.InvalidElementId));
                            validTriangles++;
                        }
                    }
                }

                if (validTriangles > 0)
                {
                    builder.CloseConnectedFaceSet();
                    builder.Target = TessellatedShapeBuilderTarget.Solid;
                    builder.Fallback = TessellatedShapeBuilderFallback.Mesh;
                    builder.Build();

                    TessellatedShapeBuilderResult buildResult = builder.GetBuildResult();
                    foreach (var gObj in buildResult.GetGeometricalObjects())
                    {
                        if (gObj is Solid s && s.Volume > 1e-7 && s.Faces.Size > 0)
                        {
                            result.Add(s);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Warning: не удалось собрать Solid из Mesh: {ex.Message}", "WARN");
            }

            return result;
        }

        private static List<Solid> BuildSolidsFrom2DContours(List<Curve> curves, double heightFeet)
        {
            var result = new List<Solid>();
            if (curves == null || curves.Count < 3) return result;

            try
            {
                var loops = AssembleCurveLoops(curves);
                foreach (var loop in loops)
                {
                    try
                    {
                        if (loop.IsOpen()) continue;
                        Solid s = GeometryCreationUtilities.CreateExtrusionGeometry(
                            new List<CurveLoop> { loop },
                            XYZ.BasisZ,
                            heightFeet);
                        if (s != null && s.Volume > 1e-7)
                        {
                            result.Add(s);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Warning: не удалось выполнить 2D выдавливание: {ex.Message}", "WARN");
            }

            return result;
        }

        private static List<CurveLoop> AssembleCurveLoops(List<Curve> rawCurves)
        {
            var loops = new List<CurveLoop>();
            var remaining = new List<Curve>(rawCurves.Where(c => c != null && c.Length > 1e-4));
            double tolerance = 0.005; // допуск смыкания точек ~1.5 мм

            while (remaining.Count > 0)
            {
                var currentLoopCurves = new List<Curve>();
                Curve current = remaining[0];
                remaining.RemoveAt(0);
                currentLoopCurves.Add(current);

                XYZ startPt = current.GetEndPoint(0);
                XYZ endPt = current.GetEndPoint(1);

                bool closed = false;
                int maxIterations = remaining.Count + 10;
                while (!closed && remaining.Count > 0 && maxIterations-- > 0)
                {
                    if (endPt.DistanceTo(startPt) < tolerance && currentLoopCurves.Count >= 3)
                    {
                        closed = true;
                        break;
                    }

                    int nextIdx = -1;
                    bool reverseNext = false;

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        Curve candidate = remaining[i];
                        if (candidate.GetEndPoint(0).DistanceTo(endPt) < tolerance)
                        {
                            nextIdx = i;
                            reverseNext = false;
                            break;
                        }
                        if (candidate.GetEndPoint(1).DistanceTo(endPt) < tolerance)
                        {
                            nextIdx = i;
                            reverseNext = true;
                            break;
                        }
                    }

                    if (nextIdx >= 0)
                    {
                        Curve nextCurve = remaining[nextIdx];
                        remaining.RemoveAt(nextIdx);

                        if (reverseNext)
                        {
                            nextCurve = nextCurve.CreateReversed();
                        }

                        currentLoopCurves.Add(nextCurve);
                        endPt = nextCurve.GetEndPoint(1);

                        if (endPt.DistanceTo(startPt) < tolerance && currentLoopCurves.Count >= 3)
                        {
                            closed = true;
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (closed && currentLoopCurves.Count >= 3)
                {
                    try
                    {
                        CurveLoop loop = CurveLoop.Create(currentLoopCurves);
                        loops.Add(loop);
                    }
                    catch { }
                }
            }

            return loops;
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
