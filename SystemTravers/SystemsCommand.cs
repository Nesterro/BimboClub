using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class SystemsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Запуск команды 'Создать виды (Системы)'.");

                // 1. Собираем системы
                List<MEPSystem> systems = new FilteredElementCollector(doc)
                    .OfClass(typeof(MEPSystem))
                    .Cast<MEPSystem>()
                    .Where(s => s.Elements.Size > 0 && (s is MechanicalSystem || s is PipingSystem))
                    .OrderBy(s => s.Name)
                    .ToList();

                if (systems.Count == 0)
                {
                    MessageBox.Show("В проекте не найдено инженерных систем с элементами.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return Result.Succeeded;
                }

                // 2. Собираем 3D виды
                List<View3D> views3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => !v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();

                // 3. Собираем спецификации
                List<ViewSchedule> schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTemplate)
                    .OrderBy(s => s.Name)
                    .ToList();

                // 4. Собираем рамки листов (TitleBlocks)
                List<FamilySymbol> titleBlocks = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .OrderBy(tb => tb.Name)
                    .ToList();

                // 5. Собираем все строковые параметры для настройки фильтров
                List<string> textParams = CollectStringParameters(doc);

                // Открываем форму
                using (FrmCreateSystemsViews form = new FrmCreateSystemsViews(uidoc, systems, views3D, schedules, titleBlocks, textParams))
                {
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Logger.LogError("Ошибка в команде 'Создать виды (Системы)'", ex);
                return Result.Failed;
            }
        }

        private List<string> CollectStringParameters(Document doc)
        {
            HashSet<string> paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_Conduit
            };

            foreach (var cat in categories)
            {
                try
                {
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    var elements = collector.OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .Take(15)
                        .ToList();

                    foreach (Element el in elements)
                    {
                        foreach (Parameter p in el.Parameters)
                        {
                            if (p.StorageType == StorageType.String)
                            {
                                paramNames.Add(p.Definition.Name);
                            }
                        }
                    }
                }
                catch { }
            }

            paramNames.Add("ADSK_Имя системы");
            paramNames.Add("Имя системы");
            return paramNames.OrderBy(x => x).ToList();
        }
    }
}
