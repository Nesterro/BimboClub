using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BimboClub.Analogs.MepAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MepAlignElevationCommand : IExternalCommand
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
            var filter = new MepAlignUtils.MepSelectionFilter(onlyCurves: false);
            int alignedCount = 0;

            try
            {
                while (true)
                {
                    // 1. Выберите опорный MEP элемент (определяет отметку)
                    Reference ref1;
                    try
                    {
                        ref1 = uiDoc.Selection.PickObject(
                            ObjectType.Element,
                            filter,
                            "Выберите опорный MEP элемент (определяет отметку высоты) [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (ref1 == null) break;
                    Element elem1 = doc.GetElement(ref1);
                    XYZ pickPt1 = ref1.GlobalPoint;

                    // 2. Выберите выравниваемый MEP элемент
                    Reference ref2;
                    try
                    {
                        ref2 = uiDoc.Selection.PickObject(
                            ObjectType.Element,
                            filter,
                            "Выберите выравниваемый MEP элемент (будет перемещен по высоте Z) [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (ref2 == null) break;
                    if (ref2.ElementId == ref1.ElementId)
                    {
                        TaskDialog.Show("Выравнивание по высоте", "Нельзя выровнять элемент относительно самого себя. Выберите другой элемент.");
                        continue;
                    }

                    Element elem2 = doc.GetElement(ref2);
                    XYZ pickPt2 = ref2.GlobalPoint;

                    if (elem2.Pinned)
                    {
                        TaskDialog.Show("Выравнивание по высоте", "Выравниваемый элемент закреплен (Pinned). Открепите его перед перемещением.");
                        continue;
                    }

                    using (Transaction tr = new Transaction(doc, "Выровнять MEP по высоте"))
                    {
                        tr.Start();
                        bool success = MepAlignUtils.AlignElevation(doc, elem1, pickPt1, elem2, pickPt2);
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

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Выравнивание по высоте", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
