using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BimboClub
{
    public class SchemeGenerator
    {
        private Document _doc;

        public SchemeGenerator(Document doc)
        {
            _doc = doc;
        }

        public void GenerateForSystem(
            string systemName, 
            string prefix, 
            string suffix, 
            List<ViewParameterConfig> parameters,
            ElementId filterParamId)
        {
            // 1. Получаем или создаем изометрический 3D-вид с именем системы
            View3D view3d = GetOrCreate3DView(systemName, prefix, suffix);

            // 2. Заполняем параметры вида
            SetViewParameters(view3d, parameters);

            // 3. Настраиваем единственный обратный фильтр видимости и скрываем не-MEP категории
            SetupViewFilters(view3d, systemName, filterParamId);

            // 4. Блокируем созданный 3D-вид для возможности нанесения аннотаций
            Lock3DView(view3d);
        }

        private View3D GetOrCreate3DView(string systemName, string prefix, string suffix)
        {
            string viewName = (prefix ?? "") + SanitizeName(systemName) + (suffix ?? "");

            // Ищем существующий вид
            View3D existingView = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));

            if (existingView != null)
            {
                return existingView;
            }

            // Находим шаблон для 3D вида
            ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

            if (viewFamilyType == null)
            {
                throw new Exception("Не найден тип 3D вида в проекте.");
            }

            // Создаем новый изометрический 3D вид
            View3D newView = View3D.CreateIsometric(_doc, viewFamilyType.Id);
            newView.Name = viewName;
            
            try
            {
                newView.Discipline = ViewDiscipline.Mechanical; // Устанавливаем дисциплину "Механика"
            }
            catch { }

            return newView;
        }

        private void SetViewParameters(View view, List<ViewParameterConfig> parameters)
        {
            if (parameters == null || parameters.Count == 0) return;

            foreach (var paramConfig in parameters)
            {
                if (string.IsNullOrEmpty(paramConfig.Name)) continue;

                // Находим параметр вида по имени
                Parameter param = view.LookupParameter(paramConfig.Name);
                if (param != null && !param.IsReadOnly)
                {
                    try
                    {
                        switch (param.StorageType)
                        {
                            case StorageType.String:
                                param.Set(paramConfig.Value ?? "");
                                break;
                            case StorageType.Integer:
                                if (int.TryParse(paramConfig.Value, out int intVal))
                                    param.Set(intVal);
                                break;
                            case StorageType.Double:
                                if (double.TryParse(paramConfig.Value, out double dbVal))
                                    param.Set(dbVal);
                                break;
                        }
                    }
                    catch { }
                }
            }
        }

        public void Lock3DView(View view)
        {
            try
            {
                if (view is View3D view3d && !view3d.IsLocked)
                {
                    view3d.SaveOrientationAndLock();
                }
            }
            catch { }
        }

        public void DeleteExistingTags(View view)
        {
            var collector = new FilteredElementCollector(_doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .ToList();

            List<ElementId> idsToDelete = collector.Select(t => t.Id).ToList();

            if (idsToDelete.Count > 0)
            {
                _doc.Delete(idsToDelete);
            }
        }

        public void PlaceAutoTags(View view, List<Element> elements, Dictionary<BuiltInCategory, ElementId> selectedTagTypes)
        {
            if (selectedTagTypes == null || selectedTagTypes.Count == 0 || elements == null) return;

            foreach (Element elem in elements)
            {
                if (elem == null) continue;

                Category cat = elem.Category;
                if (cat == null) continue;

                BuiltInCategory builtInCat = (BuiltInCategory)cat.Id.IntegerValue;

                // Если для данной категории была выбрана марка (выноска)
                if (selectedTagTypes.TryGetValue(builtInCat, out ElementId tagTypeId))
                {
                    if (tagTypeId == ElementId.InvalidElementId) continue;

                    try
                    {
                        Reference elementRef = new Reference(elem);
                        XYZ tagPoint = GetElementTagPoint(elem);

                        // Создаем марку/выноску в заблокированном 3D виде
                        IndependentTag tag = IndependentTag.Create(
                            _doc,
                            view.Id,
                            elementRef,
                            true, // С выноской (Leader = true)
                            TagMode.TM_ADDBY_CATEGORY,
                            TagOrientation.Horizontal,
                            tagPoint
                        );

                        if (tag != null)
                        {
                            // Принудительно меняем тип марки на выбранный пользователем
                            tag.ChangeTypeId(tagTypeId);
                            
                            // Устанавливаем свободный конец для выноски (Free End)
                            if (tag.CanLeaderEndConditionBeAssigned(LeaderEndCondition.Free))
                            {
                                tag.LeaderEndCondition = LeaderEndCondition.Free;
                            }

                            // Учитываем масштаб активного вида для корректного перевода миллиметров листа в футы модели
                            double scale = view.Scale;
                            if (scale < 1.0) scale = 100.0;

                            // Смещение в миллиметрах листа (как на скриншоте 2: отнесено дальше влево-вверх)
                            double mmHorizontal = -25.0; // Сдвиг влево на 25 мм
                            double mmVertical = 20.0;    // Сдвиг вверх на 20 мм
                            double mmShelf = 12.0;       // Длина полки 12 мм (полка уходит вправо от локтя)

                            // Перевод в футы: 1 мм = 0.00328084 фута
                            double footPerMm = 0.00328084;
                            double horizontalOffset = mmHorizontal * scale * footPerMm;
                            double verticalOffset = mmVertical * scale * footPerMm;
                            double shelfOffset = mmShelf * scale * footPerMm;

                            XYZ right = view.RightDirection.Normalize();
                            XYZ up = view.UpDirection.Normalize();

                            // Рассчитываем положение головки марки (отнесена влево и вверх)
                            XYZ screenOffset = (right * horizontalOffset) + (up * verticalOffset);
                            XYZ tagHeadPos = tagPoint + screenOffset;
                            tag.TagHeadPosition = tagHeadPos;

                            // Задаем точку, куда указывает стрелка выноски (на сам элемент)
                            tag.SetLeaderEnd(elementRef, tagPoint);

                            // Горизонтальная полка идет вправо от головки к локтю (в сторону элемента)
                            XYZ elbowPos = tagHeadPos + (right * shelfOffset);
                            tag.SetLeaderElbow(elementRef, elbowPos);
                        }
                    }
                    catch { }
                }
            }
        }

        private XYZ GetElementTagPoint(Element elem)
        {
            if (elem.Location is LocationCurve lc)
            {
                // Для труб и воздуховодов точка привязки - центр кривой
                return lc.Curve.Evaluate(0.5, true);
            }
            else if (elem.Location is LocationPoint lp)
            {
                // Для фитингов и оборудования - точка вставки
                return lp.Point;
            }

            BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) / 2.0;
            }

            return XYZ.Zero;
        }

        private void SetupViewFilters(View3D view, string systemName, ElementId filterParamId)
        {
            // Список всех категорий инженерных систем
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

            // Отбираем категории, которые поддерживают выбранный параметр
            List<ElementId> mepCategories = new List<ElementId>();

            foreach (var catId in rawMepCategories)
            {
                try
                {
                    var filterableParams = ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, new List<ElementId> { catId });
                    if (filterableParams.Contains(filterParamId))
                    {
                        mepCategories.Add(catId);
                    }
                }
                catch { }
            }

            // Создаем единственный ОБРАТНЫЙ фильтр
            FilterRule mepRule = ParameterFilterRuleFactory.CreateNotEqualsRule(filterParamId, systemName);
            ElementParameterFilter mepEPF = new ElementParameterFilter(mepRule);

            string mepFilterName = SanitizeName("Скрыть_все_кроме_" + systemName);
            ParameterFilterElement mepFilter = GetOrCreateFilter(mepFilterName, mepCategories, mepEPF);

            if (!view.IsFilterApplied(mepFilter.Id)) 
            {
                view.AddFilter(mepFilter.Id);
            }
            
            view.SetFilterVisibility(mepFilter.Id, false);

            // Отключаем в виде все не-MEP категории модели (Архитектура, Конструкции и др.)
            var activeMepCatIds = new HashSet<int>(rawMepCategories.Select(c => c.IntegerValue));

            foreach (Category category in _doc.Settings.Categories)
            {
                if (category.CategoryType == CategoryType.Model && category.CanAddSubcategory)
                {
                    int catId = category.Id.IntegerValue;
                    bool shouldBeVisible = activeMepCatIds.Contains(catId);

                    if (!shouldBeVisible && view.CanCategoryBeHidden(category.Id))
                    {
                        view.SetCategoryHidden(category.Id, true);
                    }
                }
            }

            // Отключаем категорию аннотаций "Уровни" (OST_Levels)
            try
            {
                ElementId levelsCatId = new ElementId(BuiltInCategory.OST_Levels);
                if (view.CanCategoryBeHidden(levelsCatId))
                {
                    view.SetCategoryHidden(levelsCatId, true);
                }
            }
            catch { }
        }

        private ParameterFilterElement GetOrCreateFilter(string name, List<ElementId> categories, ElementParameterFilter filterRule)
        {
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.SetElementFilter(filterRule);
                return existing;
            }

            return ParameterFilterElement.Create(_doc, name, categories, filterRule);
        }

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            char[] prohibitedChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            string sanitized = name;
            foreach (char c in prohibitedChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            if (sanitized.Length > 240)
            {
                sanitized = sanitized.Substring(0, 240);
            }
            return sanitized;
        }
    }
}
