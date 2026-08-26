using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAV.ParamRules;

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
                var cmd = new CmdRuler();
                return cmd.Execute(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("BimboClub | Ошибка", $"Ошибка при запуске Редактора правил:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
