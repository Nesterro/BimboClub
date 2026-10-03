using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BimboClub.Analogs.FastViewCut
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FastCrop2DCommand : IExternalCommand
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
            View view = doc.ActiveView;

            if (view == null || view is ViewSheet || view is ViewSchedule)
            {
                TaskDialog.Show("Подрезка вида", "Команда применима только к графическим 2D-видам (планы, разрезы, фасады, узлы).");
                return Result.Cancelled;
            }

            try
            {
                while (true)
                {
                    XYZ pt1;
                    try
                    {
                        pt1 = uiDoc.Selection.PickPoint(
                            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                            "Укажите первый угол рамки подрезки [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (pt1 == null) break;

                    XYZ pt2;
                    try
                    {
                        pt2 = uiDoc.Selection.PickPoint(
                            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                            "Укажите противоположный угол рамки подрезки [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (pt2 == null) break;

                    using (Transaction tr = new Transaction(doc, "Быстрая подрезка вида"))
                    {
                        tr.Start();
                        bool success = FastViewCutUtils.SetCropBoxByPoints(view, pt1, pt2);
                        if (success)
                        {
                            tr.Commit();
                        }
                        else
                        {
                            tr.RollBack();
                        }
                    }

                    // После успешной подрезки завершаем команду (или пользователь может вызвать повторно)
                    break;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Быстрая подрезка 2D", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
