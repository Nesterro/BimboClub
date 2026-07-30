using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class SetSystemNameCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Запуск команды 'Имя системы'.");

                // Собираем все доступные текстовые параметры в проекте
                List<string> textParams = CollectWritableStringParameters(doc);

                using (FrmSetSystemName form = new FrmSetSystemName(textParams))
                {
                    form.StartPosition = FormStartPosition.CenterParent;
                    if (form.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string selectedParam = form.SelectedParameter;
                    bool inFullDoc = form.InFullDoc;

                    if (string.IsNullOrEmpty(selectedParam))
                    {
                        MessageBox.Show("Параметр не выбран.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return Result.Failed;
                    }

                    // Собираем элементы
                    List<Element> mepElements = CollectMepElements(doc, uidoc.ActiveGraphicalView, inFullDoc);

                    if (mepElements.Count == 0)
                    {
                        MessageBox.Show("Не найдено подходящих MEP элементов для обработки.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return Result.Succeeded;
                    }

                    int processedCount = 0;

                    using (Transaction tr = new Transaction(doc, "Запись имени системы"))
                    {
                        tr.Start();

                        foreach (Element el in mepElements)
                        {
                            string systemName = GetElementSystemName(el);
                            if (string.IsNullOrEmpty(systemName))
                            {
                                continue;
                            }

                            Parameter targetParam = el.LookupParameter(selectedParam);
                            if (targetParam == null)
                            {
                                // Попробуем найти по GUID или BuiltInParameter, если selectedParam совпадает
                                targetParam = el.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Definition.Name.Equals(selectedParam, StringComparison.OrdinalIgnoreCase));
                            }

                            if (targetParam != null && !targetParam.IsReadOnly)
                            {
                                if (targetParam.Set(systemName))
                                {
                                    processedCount++;
                                }
                            }
                        }

                        tr.Commit();
                    }

                    MessageBox.Show($"Обработано элементов: {processedCount}.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return Result.Succeeded;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Logger.LogError("Ошибка в команде 'Имя системы'", ex);
                return Result.Failed;
            }
        }

        private List<string> CollectWritableStringParameters(Document doc)
        {
            HashSet<string> paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_CableTrayFitting,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_ConduitFitting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_PlumbingFixtures
            };

            foreach (var cat in categories)
            {
                try
                {
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    var elements = collector.OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .Take(30)
                        .ToList();

                    foreach (Element el in elements)
                    {
                        foreach (Parameter p in el.Parameters)
                        {
                            if (!p.IsReadOnly && (p.StorageType == StorageType.String))
                            {
                                paramNames.Add(p.Definition.Name);
                            }
                        }
                    }
                }
                catch { }
            }

            // Добавляем дефолтные популярные параметры, если их нет
            paramNames.Add("ADSK_Имя системы");
            paramNames.Add("ADSK_Наименование");
            paramNames.Add("Имя системы");

            return paramNames.OrderBy(x => x).ToList();
        }

        private List<Element> CollectMepElements(Document doc, Autodesk.Revit.DB.View activeView, bool inFullDoc)
        {
            List<BuiltInCategory> categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_FlexPipeCurves,
                BuiltInCategory.OST_FlexDuctCurves,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_CableTrayFitting,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_ConduitFitting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_SpecialityEquipment,
                BuiltInCategory.OST_Sprinklers,
                BuiltInCategory.OST_ElectricalEquipment,
                BuiltInCategory.OST_ElectricalFixtures,
                BuiltInCategory.OST_LightingDevices,
                BuiltInCategory.OST_LightingFixtures
            };

            List<Element> result = new List<Element>();
            foreach (var cat in categories)
            {
                try
                {
                    FilteredElementCollector collector = inFullDoc 
                        ? new FilteredElementCollector(doc) 
                        : new FilteredElementCollector(doc, activeView.Id);

                    var elements = collector.OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    result.AddRange(elements);
                }
                catch { }
            }

            return result;
        }

        private string GetElementSystemName(Element el)
        {
            // Попробуем получить через BuiltInParameter RBS_SYSTEM_NAME_PARAM
            Parameter p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            if (p != null)
            {
                string name = p.AsString();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            // Если это FamilyInstance, обойдем его SuperComponent-ы
            FamilyInstance fi = el as FamilyInstance;
            if (fi != null)
            {
                FamilyInstance super = fi.SuperComponent as FamilyInstance;
                while (super != null)
                {
                    Parameter superParam = super.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                    if (superParam != null)
                    {
                        string name = superParam.AsString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                    super = super.SuperComponent as FamilyInstance;
                }

                // Попробуем через коннекторы
                if (fi.MEPModel != null && fi.MEPModel.ConnectorManager != null)
                {
                    try
                    {
                        foreach (Connector conn in fi.MEPModel.ConnectorManager.Connectors)
                        {
                            if (conn.MEPSystem != null)
                            {
                                return conn.MEPSystem.Name;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Попробуем для MEPCurve через коннекторы
            MEPCurve curve = el as MEPCurve;
            if (curve != null && curve.ConnectorManager != null)
            {
                try
                {
                    foreach (Connector conn in curve.ConnectorManager.Connectors)
                    {
                        if (conn.MEPSystem != null)
                        {
                            return conn.MEPSystem.Name;
                        }
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
