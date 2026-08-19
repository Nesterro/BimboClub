using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace BimboClub.PipeClamps
{
    [Serializable]
    public class PipeClampSavedSymbolSetting
    {
        public int DiameterMm { get; set; }
        public string SelectedSymbolDisplayName { get; set; }
        public string StepMm { get; set; } = "1500";
        public string OffsetMm { get; set; } = "300";
    }

    [Serializable]
    public class PipeClampsSettings
    {
        public bool ActiveViewOnly { get; set; } = false;

        public bool CopyParam1 { get; set; } = true;
        public string SourceParam1 { get; set; } = "ADSK_Номер стояка";
        public string TargetParam1 { get; set; } = "ADSK_Номер стояка";

        public bool CopyParam2 { get; set; } = true;
        public string SourceParam2 { get; set; } = "ADSK_Секция";
        public string TargetParam2 { get; set; } = "ADSK_Секция";

        public string RotateXDeg { get; set; } = "0";
        public string RotateYDeg { get; set; } = "0";
        public string RotateZDeg { get; set; } = "0";

        public List<PipeClampSavedSymbolSetting> SavedSymbols { get; set; } = new List<PipeClampSavedSymbolSetting>();

        private static string GetFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "BimboClub");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "PipeClampsSettings.xml");
        }

        public static PipeClampsSettings Load()
        {
            try
            {
                string path = GetFilePath();
                if (File.Exists(path))
                {
                    var xs = new XmlSerializer(typeof(PipeClampsSettings));
                    using (var stream = File.OpenRead(path))
                    {
                        var settings = (PipeClampsSettings)xs.Deserialize(stream);
                        if (settings != null) return settings;
                    }
                }
            }
            catch { }
            return new PipeClampsSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetFilePath();
                var xs = new XmlSerializer(typeof(PipeClampsSettings));
                using (var stream = File.Create(path))
                {
                    xs.Serialize(stream, this);
                }
            }
            catch { }
        }
    }
}
