using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ParamRulesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Run native built-in Rule Editor (FrmRuler) directly from BimboClub engine
                SAV.ParamRules.CmdRuler rulerCommand = new SAV.ParamRules.CmdRuler();
                return rulerCommand.Execute(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Autodesk.Revit.UI.TaskDialog.Show("BimboClub | Ошибка", $"Ошибка при запуске Редактора правил:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
