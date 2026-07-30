using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class TagCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            if (activeView == null)
            {
                TaskDialog.Show("Ошибка", "Не найден активный вид.");
                return Result.Failed;
            }

            try
            {
                Logger.Log($"Запуск авто-маркировки Auto Tags. Активный вид: '{activeView.Name}' (ID: {activeView.Id}).");
                // 1. Собираем загруженные типы выносок (марок)
                var ductTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_DuctTags);
                var pipeTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_PipeTags);
                var ductFittingTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_DuctFittingTags);
                var pipeFittingTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_PipeFittingTags);
                var equipmentTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_MechanicalEquipmentTags);
                var ductAccessoryTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_DuctAccessoryTags);
                var pipeAccessoryTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_PipeAccessoryTags);
                var terminalTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_DuctTerminalTags);
                var cableTrayTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_CableTrayTags);
                var cableTrayFittingTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_CableTrayFittingTags);
                var conduitTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_ConduitTags);
                var conduitFittingTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_ConduitFittingTags);

                var sprinklerTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_SprinklerTags);
                var plumbingFixtureTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_PlumbingFixtureTags);
                var lightingFixtureTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_LightingFixtureTags);
                var electricalEquipmentTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_ElectricalEquipmentTags);
                var electricalFixtureTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_ElectricalFixtureTags);
                var fireAlarmDeviceTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_FireAlarmDeviceTags);
                var flexDuctTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_FlexDuctTags);
                var flexPipeTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_FlexPipeTags);
                var windowTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_WindowTags);
                var doorTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_DoorTags);
                var wallTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_WallTags);
                var genericModelTags = GetTagTypesForCategory(doc, BuiltInCategory.OST_GenericModelTags);

                // 2. Создаем конфигурацию категорий для окна UI
                List<CategoryTagConfig> configs = new List<CategoryTagConfig>
                {
                    new CategoryTagConfig
                    {
                        CategoryName = "Воздуховоды",
                        Category = BuiltInCategory.OST_DuctCurves,
                        AvailableTags = ductTags,
                        SelectedTag = ductTags.Count > 1 ? ductTags[1] : ductTags.FirstOrDefault(),
                        IsChecked = ductTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Трубопроводы",
                        Category = BuiltInCategory.OST_PipeCurves,
                        AvailableTags = pipeTags,
                        SelectedTag = pipeTags.Count > 1 ? pipeTags[1] : pipeTags.FirstOrDefault(),
                        IsChecked = pipeTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Фитинги воздуховодов",
                        Category = BuiltInCategory.OST_DuctFitting,
                        AvailableTags = ductFittingTags,
                        SelectedTag = ductFittingTags.Count > 1 ? ductFittingTags[1] : ductFittingTags.FirstOrDefault(),
                        IsChecked = ductFittingTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Фитинги трубопроводов",
                        Category = BuiltInCategory.OST_PipeFitting,
                        AvailableTags = pipeFittingTags,
                        SelectedTag = pipeFittingTags.Count > 1 ? pipeFittingTags[1] : pipeFittingTags.FirstOrDefault(),
                        IsChecked = pipeFittingTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Оборудование",
                        Category = BuiltInCategory.OST_MechanicalEquipment,
                        AvailableTags = equipmentTags,
                        SelectedTag = equipmentTags.Count > 1 ? equipmentTags[1] : equipmentTags.FirstOrDefault(),
                        IsChecked = equipmentTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Арматура воздуховодов",
                        Category = BuiltInCategory.OST_DuctAccessory,
                        AvailableTags = ductAccessoryTags,
                        SelectedTag = ductAccessoryTags.Count > 1 ? ductAccessoryTags[1] : ductAccessoryTags.FirstOrDefault(),
                        IsChecked = ductAccessoryTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Арматура трубопроводов",
                        Category = BuiltInCategory.OST_PipeAccessory,
                        AvailableTags = pipeAccessoryTags,
                        SelectedTag = pipeAccessoryTags.Count > 1 ? pipeAccessoryTags[1] : pipeAccessoryTags.FirstOrDefault(),
                        IsChecked = pipeAccessoryTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Воздухораспределители",
                        Category = BuiltInCategory.OST_DuctTerminal,
                        AvailableTags = terminalTags,
                        SelectedTag = terminalTags.Count > 1 ? terminalTags[1] : terminalTags.FirstOrDefault(),
                        IsChecked = terminalTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Кабельные лотки",
                        Category = BuiltInCategory.OST_CableTray,
                        AvailableTags = cableTrayTags,
                        SelectedTag = cableTrayTags.Count > 1 ? cableTrayTags[1] : cableTrayTags.FirstOrDefault(),
                        IsChecked = cableTrayTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Фитинги кабельных лотков",
                        Category = BuiltInCategory.OST_CableTrayFitting,
                        AvailableTags = cableTrayFittingTags,
                        SelectedTag = cableTrayFittingTags.Count > 1 ? cableTrayFittingTags[1] : cableTrayFittingTags.FirstOrDefault(),
                        IsChecked = cableTrayFittingTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Короба (Кабель-каналы)",
                        Category = BuiltInCategory.OST_Conduit,
                        AvailableTags = conduitTags,
                        SelectedTag = conduitTags.Count > 1 ? conduitTags[1] : conduitTags.FirstOrDefault(),
                        IsChecked = conduitTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Фитинги коробов",
                        Category = BuiltInCategory.OST_ConduitFitting,
                        AvailableTags = conduitFittingTags,
                        SelectedTag = conduitFittingTags.Count > 1 ? conduitFittingTags[1] : conduitFittingTags.FirstOrDefault(),
                        IsChecked = conduitFittingTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Спринклеры",
                        Category = BuiltInCategory.OST_Sprinklers,
                        AvailableTags = sprinklerTags,
                        SelectedTag = sprinklerTags.Count > 1 ? sprinklerTags[1] : sprinklerTags.FirstOrDefault(),
                        IsChecked = sprinklerTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Сантехнические приборы",
                        Category = BuiltInCategory.OST_PlumbingFixtures,
                        AvailableTags = plumbingFixtureTags,
                        SelectedTag = plumbingFixtureTags.Count > 1 ? plumbingFixtureTags[1] : plumbingFixtureTags.FirstOrDefault(),
                        IsChecked = plumbingFixtureTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Осветительные приборы",
                        Category = BuiltInCategory.OST_LightingFixtures,
                        AvailableTags = lightingFixtureTags,
                        SelectedTag = lightingFixtureTags.Count > 1 ? lightingFixtureTags[1] : lightingFixtureTags.FirstOrDefault(),
                        IsChecked = lightingFixtureTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Электрооборудование",
                        Category = BuiltInCategory.OST_ElectricalEquipment,
                        AvailableTags = electricalEquipmentTags,
                        SelectedTag = electricalEquipmentTags.Count > 1 ? electricalEquipmentTags[1] : electricalEquipmentTags.FirstOrDefault(),
                        IsChecked = electricalEquipmentTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Электрические приборы",
                        Category = BuiltInCategory.OST_ElectricalFixtures,
                        AvailableTags = electricalFixtureTags,
                        SelectedTag = electricalFixtureTags.Count > 1 ? electricalFixtureTags[1] : electricalFixtureTags.FirstOrDefault(),
                        IsChecked = electricalFixtureTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Пожарная сигнализация",
                        Category = BuiltInCategory.OST_FireAlarmDevices,
                        AvailableTags = fireAlarmDeviceTags,
                        SelectedTag = fireAlarmDeviceTags.Count > 1 ? fireAlarmDeviceTags[1] : fireAlarmDeviceTags.FirstOrDefault(),
                        IsChecked = fireAlarmDeviceTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Гибкие воздуховоды",
                        Category = BuiltInCategory.OST_FlexDuctCurves,
                        AvailableTags = flexDuctTags,
                        SelectedTag = flexDuctTags.Count > 1 ? flexDuctTags[1] : flexDuctTags.FirstOrDefault(),
                        IsChecked = flexDuctTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Гибкие трубы",
                        Category = BuiltInCategory.OST_FlexPipeCurves,
                        AvailableTags = flexPipeTags,
                        SelectedTag = flexPipeTags.Count > 1 ? flexPipeTags[1] : flexPipeTags.FirstOrDefault(),
                        IsChecked = flexPipeTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Окна",
                        Category = BuiltInCategory.OST_Windows,
                        AvailableTags = windowTags,
                        SelectedTag = windowTags.Count > 1 ? windowTags[1] : windowTags.FirstOrDefault(),
                        IsChecked = windowTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Двери",
                        Category = BuiltInCategory.OST_Doors,
                        AvailableTags = doorTags,
                        SelectedTag = doorTags.Count > 1 ? doorTags[1] : doorTags.FirstOrDefault(),
                        IsChecked = doorTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Стены",
                        Category = BuiltInCategory.OST_Walls,
                        AvailableTags = wallTags,
                        SelectedTag = wallTags.Count > 1 ? wallTags[1] : wallTags.FirstOrDefault(),
                        IsChecked = wallTags.Count > 1
                    },
                    new CategoryTagConfig
                    {
                        CategoryName = "Обобщенные модели",
                        Category = BuiltInCategory.OST_GenericModel,
                        AvailableTags = genericModelTags,
                        SelectedTag = genericModelTags.Count > 1 ? genericModelTags[1] : genericModelTags.FirstOrDefault(),
                        IsChecked = genericModelTags.Count > 1
                    }
                };

                // 3. Открываем окно настройки выносок
                TagSelectionWindow tagWindow = new TagSelectionWindow(configs, doc, activeView);
                var helper = new System.Windows.Interop.WindowInteropHelper(tagWindow);
                helper.Owner = uiapp.MainWindowHandle;
                bool? tagResult = tagWindow.ShowDialog();
                
                if (tagResult != true)
                {
                    return Result.Cancelled;
                }

                // 4. Фильтруем выбранные пользователем настройки
                var checkedConfigs = configs.Where(c => c.IsChecked && c.SelectedTag != null && c.SelectedTag.Id != ElementId.InvalidElementId).ToList();
                if (checkedConfigs.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Не выбрано ни одной категории или марки для расстановки.");
                    return Result.Cancelled;
                }

                // Создаем карту соответствия категорий и их выбранных ID типов марок
                Dictionary<BuiltInCategory, ElementId> selectedTagTypes = checkedConfigs.ToDictionary(c => c.Category, c => c.SelectedTag.Id);

                // 5. Собираем все видимые элементы выбранных категорий на активном виде
                List<BuiltInCategory> activeCats = checkedConfigs.Select(c => c.Category).ToList();
                ElementMulticategoryFilter catFilter = new ElementMulticategoryFilter(activeCats);
                
                var elementsInView = new FilteredElementCollector(doc, activeView.Id)
                    .WherePasses(catFilter)
                    .WhereElementIsNotElementType()
                    .ToList();

                // Фильтрация элементов по выбранным семействам из списка
                var selectedFamiliesSet = new HashSet<string>(tagWindow.SelectedFamilies, StringComparer.OrdinalIgnoreCase);
                elementsInView = elementsInView.Where(el =>
                {
                    string famName = "";
                    ElementId typeId = el.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        ElementType elType = doc.GetElement(typeId) as ElementType;
                        if (elType != null)
                        {
                            try { famName = elType.FamilyName ?? ""; } catch { }
                        }
                    }

                    if (string.IsNullOrEmpty(famName) && el is FamilyInstance fi && fi.Symbol?.Family != null)
                    {
                        famName = fi.Symbol.Family.Name ?? "";
                    }

                    if (string.IsNullOrEmpty(famName))
                    {
                        famName = el.Name ?? "";
                    }

                    return selectedFamiliesSet.Contains(famName);
                }).ToList();

                if (elementsInView.Count == 0)
                {
                    TaskDialog.Show("Внимание", "На текущем виде не найдено видимых элементов выбранных категорий (с учетом фильтра по имени семейства/типа).");
                    return Result.Cancelled;
                }

                // 6. Расставляем марки в рамках транзакции
                using (Transaction tx = new Transaction(doc, "Автоматическая расстановка выносок"))
                {
                    tx.Start();

                    SchemeGenerator generator = new SchemeGenerator(doc);

                    // Если активный вид - 3D, блокируем его (Revit требует этого для аннотаций в 3D)
                    if (activeView is View3D view3D)
                    {
                        generator.Lock3DView(view3D);
                    }

                    // Удаляем старые марки на активном виде, чтобы не было наложения
                    generator.DeleteExistingTags(activeView);

                    // Автоматически расставляем новые выноски/марки
                    generator.PlaceAutoTags(activeView, elementsInView, selectedTagTypes);

                    tx.Commit();
                }

                Logger.Log($"Успешно расставлено марок. Обработано элементов: {elementsInView.Count}.");
                TaskDialog.Show("Успех", $"Выноски успешно расставлены!\nОбработано элементов: {elementsInView.Count}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время авто-маркировки Auto Tags", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private List<TagTypeItem> GetTagTypesForCategory(Document doc, BuiltInCategory tagCategory)
        {
            var list = new List<TagTypeItem>();
            list.Add(new TagTypeItem { DisplayName = "<Не подписывать>", Id = ElementId.InvalidElementId });

            var loadedTags = new FilteredElementCollector(doc)
                .OfCategory(tagCategory)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Select(t => new TagTypeItem
                {
                    DisplayName = t.Family.Name + " : " + t.Name,
                    Id = t.Id
                })
                .OrderBy(item => item.DisplayName)
                .ToList();

            list.AddRange(loadedTags);
            return list;
        }
    }
}
