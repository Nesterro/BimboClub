using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BimboClub.Analogs.FastViewCut
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FastCrop3DCommand : IExternalCommand
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
                TaskDialog.Show("3D Подрезка", "Пожалуйста, откройте план этажа или координационный вид для указания рамки.");
                return Result.Cancelled;
            }

            try
            {
                XYZ pt1;
                try
                {
                    pt1 = uiDoc.Selection.PickPoint(
                        ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                        "Укажите первый угол фрагмента на плане для 3D сечения [ESC для отмены]");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (pt1 == null) return Result.Cancelled;

                XYZ pt2;
                try
                {
                    pt2 = uiDoc.Selection.PickPoint(
                        ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                        "Укажите противоположный угол фрагмента на плане [ESC для отмены]");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (pt2 == null) return Result.Cancelled;

                View3D target3D = null;
                using (Transaction tr = new Transaction(doc, "3D сечение по плану"))
                {
                    tr.Start();
                    target3D = FastViewCutUtils.CreateOrUpdateSectionBox3D(doc, view, pt1, pt2);
                    if (target3D != null)
                    {
                        tr.Commit();
                    }
                    else
                    {
                        tr.RollBack();
                    }
                }

                if (target3D != null)
                {
                    uiDoc.ActiveView = target3D;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде 3D Подрезка плана", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
