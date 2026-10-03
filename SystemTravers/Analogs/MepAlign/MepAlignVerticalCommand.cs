using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BimboClub.Analogs.MepAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MepAlignVerticalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Нет активного документа Revit.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            var filter = new MepAlignUtils.MepSelectionFilter(onlyCurves: true);

            try
            {
                // Проверяем, есть ли уже предварительно выбранные элементы
                var selectedIds = uiDoc.Selection.GetElementIds();
                List<MEPCurve> curvesToProcess = new List<MEPCurve>();

                if (selectedIds != null && selectedIds.Count > 0)
                {
                    foreach (ElementId id in selectedIds)
                    {
                        if (doc.GetElement(id) is MEPCurve mepCurve)
                        {
                            curvesToProcess.Add(mepCurve);
                        }
                    }
                }

                if (curvesToProcess.Count > 0)
                {
                    int fixedCount = 0;
                    using (Transaction tr = new Transaction(doc, "Выровнять стояки вертикально"))
                    {
                        tr.Start();
                        foreach (MEPCurve curve in curvesToProcess)
                        {
                            if (!curve.Pinned && MepAlignUtils.AlignVertical(doc, curve))
                            {
                                fixedCount++;
                            }
                        }
                        tr.Commit();
                    }

                    TaskDialog.Show("Выравнивание стояков", $"Обработано вертикальных участков: {fixedCount} из {curvesToProcess.Count}.");
                    return Result.Succeeded;
                }

                // Интерактивный цикл выбора стояков
                int alignedCount = 0;
                while (true)
                {
                    Reference refCurve;
                    try
                    {
                        refCurve = uiDoc.Selection.PickObject(
                            ObjectType.Element,
                            filter,
                            "Выберите стояк/MEP кривую для выравнивания строго по вертикали [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (refCurve == null) break;
                    if (doc.GetElement(refCurve) is MEPCurve mep)
                    {
                        if (mep.Pinned)
                        {
                            TaskDialog.Show("Выравнивание стояка", "Выбранный элемент закреплен (Pinned). Открепите его.");
                            continue;
                        }

                        using (Transaction tr = new Transaction(doc, "Выровнять стояк вертикально"))
                        {
                            tr.Start();
                            bool success = MepAlignUtils.AlignVertical(doc, mep);
                            if (success)
                            {
                                tr.Commit();
                                alignedCount++;
                            }
                            else
                            {
                                tr.RollBack();
                            }
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Выравнивание вертикально", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
