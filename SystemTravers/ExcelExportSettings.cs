using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace BimboClub
{
    public class ScheduleSet
    {
        public string Name { get; set; }
        public List<string> SelectedScheduleNames { get; set; } = new List<string>();
    }

    public class ExportConfig
    {
        public string Name { get; set; }
        public string ExportFolder { get; set; }
        public bool EachScheduleInSeparateFile { get; set; } = true;
        public string FilePrefix { get; set; } = "";
        public string FileName { get; set; } = "Спецификации";
        public bool ExportHeader { get; set; } = true;
        public bool PreserveFormatting { get; set; } = true;
    }

    public class ExcelExportSettings
    {
        public string ExportFolder { get; set; } = "";
        public bool EachScheduleInSeparateFile { get; set; } = true;
        public string FilePrefix { get; set; } = "";
        public string FileName { get; set; } = "Спецификации";
        public bool ExportHeader { get; set; } = true;
        public bool PreserveFormatting { get; set; } = true;

        public List<ScheduleSet> Sets { get; set; } = new List<ScheduleSet>();
        public string SelectedSet { get; set; } = "";

        public List<ExportConfig> Configs { get; set; } = new List<ExportConfig>();
        public string SelectedConfig { get; set; } = "";

        private static string GetFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "BimboClub");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "excel_export_settings.xml");
        }

        public static ExcelExportSettings Load()
        {
            string path = GetFilePath();
            if (!File.Exists(path))
            {
                var defaultSettings = new ExcelExportSettings();
                // Add some initial empty config/set
                defaultSettings.Configs.Add(new ExportConfig { Name = "Конфигурация 1" });
                defaultSettings.SelectedConfig = "Конфигурация 1";
                return defaultSettings;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(ExcelExportSettings));
                using (var reader = new StreamReader(path))
                {
                    return (ExcelExportSettings)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return new ExcelExportSettings();
            }
        }

        public void Save()
        {
            try
            {
                string path = GetFilePath();
                var serializer = new XmlSerializer(typeof(ExcelExportSettings));
                using (var writer = new StreamWriter(path))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch
            {
                // Ignored
            }
        }
    }
}
