using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class SizerCommand : IExternalCommand
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
                Logger.Log("Запуск команды Расчет сечений (Sizer).");

                // 1. Проверяем выбор пользователя
                var selectedIds = uidoc.Selection.GetElementIds();
                List<MEPCurve> selectedMep = new List<MEPCurve>();

                foreach (var id in selectedIds)
                {
                    Element elem = doc.GetElement(id);
                    if (elem is MEPCurve mep)
                    {
                        selectedMep.Add(mep);
                    }
                }

                bool hasSelection = selectedMep.Count > 0;
                string categoryName = "";
                double initialFlow = 0;
                double initialSizeVal1 = 0; // диаметр или ширина
                double initialSizeVal2 = 0; // высота (если прямоугольный)

                if (hasSelection)
                {
                    MEPCurve primary = selectedMep.First();
                    categoryName = primary.Category.Name;

                    // Пытаемся считать расход (Revit хранит расход в внутренних единицах - кубических футах в секунду CFS)
                    Parameter flowParam = null;
                    if (primary is Duct)
                    {
                        flowParam = primary.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                    }
                    else if (primary is Pipe)
                    {
                        flowParam = primary.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM);
                    }

                    if (flowParam != null && flowParam.HasValue)
                    {
                        double internalFlow = flowParam.AsDouble();
                        if (primary is Duct)
                        {
                            // 1 CFS = 101.940647 м3/ч
                            initialFlow = internalFlow * 101.940647;
                        }
                        else
                        {
                            // 1 CFS = 28.3168466 л/с
                            initialFlow = internalFlow * 28.3168466;
                        }
                    }

                    // Считываем размеры (Revit хранит во внутренних единицах - футах)
                    if (primary is Duct duct)
                    {
                        Parameter dParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                        Parameter wParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                        Parameter hParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

                        if (dParam != null && dParam.HasValue)
                        {
                            initialSizeVal1 = dParam.AsDouble() * 304.8;
                        }
                        else if (wParam != null && hParam != null && wParam.HasValue && hParam.HasValue)
                        {
                            initialSizeVal1 = wParam.AsDouble() * 304.8;
                            initialSizeVal2 = hParam.AsDouble() * 304.8;
                        }
                    }
                    else if (primary is Pipe pipe)
                    {
                        Parameter dParam = pipe.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                        if (dParam != null) initialSizeVal1 = dParam.AsDouble() * 304.8;
                    }
                }

                // 2. Открываем окно калькулятора
                SizerWindow window = new SizerWindow(hasSelection, categoryName, initialFlow, initialSizeVal1, initialSizeVal2);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true && window.IsApplied && hasSelection)
                {
                    int successCount = 0;
                    using (Transaction tx = new Transaction(doc, "Расчет сечений BimboClub"))
                    {
                        tx.Start();

                        foreach (MEPCurve mep in selectedMep)
                        {
                            try
                            {
                                if (mep is Duct duct)
                                {
                                    Parameter dParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                                    Parameter wParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                                    Parameter hParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

                                    if (dParam != null && !dParam.IsReadOnly && window.FinalDiameter > 0)
                                    {
                                        dParam.Set(window.FinalDiameter / 304.8);
                                        successCount++;
                                    }
                                    else if (wParam != null && !wParam.IsReadOnly && hParam != null && !hParam.IsReadOnly && window.FinalWidth > 0 && window.FinalHeight > 0)
                                    {
                                        wParam.Set(window.FinalWidth / 304.8);
                                        hParam.Set(window.FinalHeight / 304.8);
                                        successCount++;
                                    }
                                }
                                else if (mep is Pipe pipe && window.FinalDiameter > 0)
                                {
                                    Parameter dParam = pipe.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                                    if (dParam != null && !dParam.IsReadOnly)
                                    {
                                        dParam.Set(window.FinalDiameter / 304.8);
                                        successCount++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Не удалось изменить размер элемента {mep.Id}", ex);
                            }
                        }

                        tx.Commit();
                    }

                    Logger.Log($"Успешно пересчитаны сечения для элементов: {successCount} из {selectedMep.Count}.");
                    TaskDialog.Show("Успех", $"Сечения успешно пересчитаны!\nИзменено элементов: {successCount} из {selectedMep.Count}");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения SizerCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
