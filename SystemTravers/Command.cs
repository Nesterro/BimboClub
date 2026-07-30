using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
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
                Logger.Log("Запуск команды 3D Scheme (создание 3D схем).");
                // 1. Собираем системы напрямую по классам
                var mechSystems = new FilteredElementCollector(doc)
                    .OfClass(typeof(MechanicalSystem))
                    .Cast<MEPSystem>()
                    .ToList();

                var pipingSystems = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipingSystem))
                    .Cast<MEPSystem>()
                    .ToList();

                var allSystems = mechSystems.Concat(pipingSystems)
                    .OrderBy(s => s.Name)
                    .ToList();

                // 2. Наполняем список систем для UI
                List<SystemItem> systemItems = new List<SystemItem>();
                foreach (var sys in allSystems)
                {
                    if (sys == null || string.IsNullOrEmpty(sys.Name)) continue;

                    if (sys.Name.Equals("Undefined", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var sysElements = GetElementsInSystem(doc, sys);
                    
                    systemItems.Add(new SystemItem
                    {
                        Name = sys.Name,
                        ElementCount = sysElements.Count,
                        TypeName = sys is MechanicalSystem ? "Воздуховод" : "Трубопровод",
                        SystemObj = sys,
                        IsChecked = false
                    });
                }

                if (systemItems.Count == 0)
                {
                    TaskDialog.Show("Диагностика", "Не найдено систем для отображения в модели.");
                    return Result.Cancelled;
                }

                // 2.5 Собираем доступные параметры для фильтрации
                List<ElementId> rawMepCategories = new List<ElementId>
                {
                    new ElementId(BuiltInCategory.OST_DuctCurves),
                    new ElementId(BuiltInCategory.OST_PlaceHolderDucts),
                    new ElementId(BuiltInCategory.OST_DuctFitting),
                    new ElementId(BuiltInCategory.OST_DuctAccessory),
                    new ElementId(BuiltInCategory.OST_DuctTerminal),
                    new ElementId(BuiltInCategory.OST_PipeCurves),
                    new ElementId(BuiltInCategory.OST_PlaceHolderPipes),
                    new ElementId(BuiltInCategory.OST_PipeFitting),
                    new ElementId(BuiltInCategory.OST_PipeAccessory),
                    new ElementId(BuiltInCategory.OST_FlexDuctCurves),
                    new ElementId(BuiltInCategory.OST_FlexPipeCurves),
                    new ElementId(BuiltInCategory.OST_MechanicalEquipment),
                    new ElementId(BuiltInCategory.OST_PlumbingFixtures),
                    new ElementId(BuiltInCategory.OST_Sprinklers),
                    new ElementId(BuiltInCategory.OST_DuctInsulations),
                    new ElementId(BuiltInCategory.OST_PipeInsulations),
                    new ElementId(BuiltInCategory.OST_GenericModel)
                };

                List<FilterParameterItem> filterParams = new List<FilterParameterItem>();
                try
                {
                    var filterableParamIds = ParameterFilterUtilities.GetFilterableParametersInCommon(doc, rawMepCategories);
                    foreach (ElementId paramId in filterableParamIds)
                    {
                        string name = "";
                        if (paramId.IntegerValue < 0)
                        {
                            try { name = LabelUtils.GetLabelFor((BuiltInParameter)paramId.IntegerValue); } catch { }
                        }
                        else
                        {
                            Element elem = doc.GetElement(paramId);
                            if (elem != null) name = elem.Name;
                        }
                        if (!string.IsNullOrEmpty(name))
                        {
                            filterParams.Add(new FilterParameterItem { Id = paramId, Name = name });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка получения общих фильтруемых параметров", ex);
                }

                // Добавляем дефолтный параметр System Name (Имя системы) если его нет
                ElementId systemNameParamId = new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (!filterParams.Any(p => p.Id == systemNameParamId))
                {
                    try
                    {
                        filterParams.Add(new FilterParameterItem 
                        { 
                            Id = systemNameParamId, 
                            Name = LabelUtils.GetLabelFor(BuiltInParameter.RBS_SYSTEM_NAME_PARAM) 
                        });
                    }
                    catch 
                    {
                        filterParams.Add(new FilterParameterItem 
                        { 
                            Id = systemNameParamId, 
                            Name = "Имя системы" 
                        });
                    }
                }

                filterParams = filterParams.OrderBy(p => p.Name).ToList();

                // 3. Открываем первое окно: Выбор систем
                SystemSelectionWindow window = new SystemSelectionWindow(systemItems, filterParams);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;
                bool? dialogResult = window.ShowDialog();

                if (dialogResult != true)
                {
                    return Result.Cancelled;
                }

                var selectedItems = window.SelectedSystems;
                Logger.Log($"Пользователь выбрал {selectedItems.Count} систем для генерации 3D схем.");
                int processedCount = 0;

                // 4. Генерация 3D видов с фильтрами (без выносок)
                using (Transaction tx = new Transaction(doc, "Создание 3D схем"))
                {
                    tx.Start();

                    SchemeGenerator generator = new SchemeGenerator(doc);

                    foreach (var item in selectedItems)
                    {
                        MEPSystem systemObj = item.SystemObj as MEPSystem;
                        if (systemObj == null) continue;

                        var sysElements = GetElementsInSystem(doc, systemObj);
                        if (sysElements.Count == 0) continue;

                        // Запускаем генерацию вида с учетом префикса/суффикса, заполняем параметры вида и настраиваем фильтры
                        generator.GenerateForSystem(
                            systemObj.Name, 
                            window.ViewPrefix, 
                            window.ViewSuffix, 
                            window.ViewParameters,
                            window.SelectedFilterParamId
                        );
                        processedCount++;
                    }

                    tx.Commit();
                }

                Logger.Log($"Успешно создано видов: {processedCount} из {selectedItems.Count}.");
                TaskDialog.Show("Успех", $"Обработка завершена!\nУспешно создано 3D-видов: {processedCount} из {selectedItems.Count}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка при выполнении 3D Scheme", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private List<Element> GetElementsInSystem(Document doc, MEPSystem system)
        {
            List<Element> elements = new List<Element>();

            foreach (Element elem in system.Elements)
            {
                if (elem != null) elements.Add(elem);
            }

            if (elements.Count > 0)
            {
                return elements;
            }

            string sysName = system.Name;

            var collector = new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_PlaceHolderDucts,
                    BuiltInCategory.OST_DuctFitting,
                    BuiltInCategory.OST_DuctAccessory,
                    BuiltInCategory.OST_DuctTerminal,
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_PlaceHolderPipes,
                    BuiltInCategory.OST_PipeFitting,
                    BuiltInCategory.OST_PipeAccessory,
                    BuiltInCategory.OST_MechanicalEquipment,
                    BuiltInCategory.OST_PlumbingFixtures,
                    BuiltInCategory.OST_Sprinklers,
                    BuiltInCategory.OST_DuctInsulations,
                    BuiltInCategory.OST_PipeInsulations
                }))
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var elem in collector)
            {
                var param = elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (param != null)
                {
                    string val = param.AsString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        if (val.IndexOf(sysName, StringComparison.OrdinalIgnoreCase) >= 0 || 
                            sysName.IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            elements.Add(elem);
                        }
                    }
                }
            }

            return elements;
        }
    }
}
