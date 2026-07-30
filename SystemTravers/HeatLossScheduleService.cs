using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.HeatLoss
{
    public static class HeatLossScheduleService
    {
        public const string SCHEDULE_NAME = "Теплопотери - Ограждающие конструкции";

        public static ViewSchedule EnsureScheduleExists(Document doc)
        {
            if (doc == null) return null;

            // Проверяем, существует ли уже спецификация
            ViewSchedule schedule = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(s => !s.IsTemplate && s.Name.Equals(SCHEDULE_NAME, StringComparison.OrdinalIgnoreCase));

            if (schedule != null)
            {
                return schedule;
            }

            // Создаем новую спецификацию по категории Обобщенные модели (OST_GenericModel)
            ElementId categoryId = new ElementId(BuiltInCategory.OST_GenericModel);
            schedule = ViewSchedule.CreateSchedule(doc, categoryId);
            schedule.Name = SCHEDULE_NAME;

            // Добавляем поля параметров
            AddScheduleField(doc, schedule, "HL_Room_Number", "Номер помещения");
            AddScheduleField(doc, schedule, "HL_Room_Name", "Имя помещения");
            AddScheduleField(doc, schedule, "HL_Element_Category", "Категория конструкции");
            AddScheduleField(doc, schedule, "HL_Area", "Площадь (м²)");
            AddScheduleField(doc, schedule, "HL_Orientation_Azimuth", "Азимут (°)");
            AddScheduleField(doc, schedule, "HL_Is_External", "Наружная (1/0)");
            AddScheduleField(doc, schedule, "HL_Element_Id", "ID элемента");

            return schedule;
        }

        private static void AddScheduleField(Document doc, ViewSchedule schedule, string paramName, string columnHeading)
        {
            ScheduleDefinition def = schedule.Definition;
            SchedulableField schedField = def.GetSchedulableFields()
                .FirstOrDefault(sf => sf.GetName(doc).Equals(paramName, StringComparison.OrdinalIgnoreCase));

            if (schedField != null)
            {
                ScheduleField field = def.AddField(schedField);
                if (!string.IsNullOrEmpty(columnHeading))
                {
                    field.ColumnHeading = columnHeading;
                }
            }
        }
    }
}
