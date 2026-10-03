using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BimboClub.Analogs.BaseLevel
{
    public enum LevelChoiceMode
    {
        Nearest,        // Ближайший
        NearestBelow,   // Ближайший снизу
        NearestAbove,   // Ближайший сверху
        Keep,           // Не изменять
        Unbind,         // Не привязывать / Отвязать
        Specific        // Конкретный уровень
    }

    public enum ProcessScope
    {
        SelectedElements,
        ActiveView,
        EntireModel
    }

    [DataContract]
    public class BaseLevelRule
    {
        [DataMember]
        public string CategoryName { get; set; } = "Все категории";

        [DataMember]
        public string ParameterName { get; set; } = "";

        [DataMember]
        public string ParameterValue { get; set; } = "";

        [DataMember]
        public LevelChoiceMode BaseMode { get; set; } = LevelChoiceMode.NearestBelow;

        [DataMember]
        public long SpecificBaseLevelId { get; set; } = -1;

        [DataMember]
        public LevelChoiceMode TopMode { get; set; } = LevelChoiceMode.Keep;

        [DataMember]
        public long SpecificTopLevelId { get; set; } = -1;
    }

    [DataContract]
    public class BaseLevelConfiguration
    {
        [DataMember]
        public string Name { get; set; } = "По умолчанию";

        [DataMember]
        public List<BaseLevelRule> Rules { get; set; } = new List<BaseLevelRule>();
    }

    [DataContract]
    public class BaseLevelStorage
    {
        [DataMember]
        public List<BaseLevelConfiguration> Configurations { get; set; } = new List<BaseLevelConfiguration>();

        [DataMember]
        public string SelectedConfigurationName { get; set; } = "По умолчанию";

        private static string GetConfigFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "BimboClub");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "BaseLevelPresets.json");
        }

        public static BaseLevelStorage Load()
        {
            string path = GetConfigFilePath();
            try
            {
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(BaseLevelStorage));
                        var loaded = serializer.ReadObject(stream) as BaseLevelStorage;
                        if (loaded != null && loaded.Configurations != null && loaded.Configurations.Count > 0)
                        {
                            return loaded;
                        }
                    }
                }
            }
            catch { }

            // Конфигурация по умолчанию
            var storage = new BaseLevelStorage();
            var defConfig = new BaseLevelConfiguration { Name = "Основная" };
            defConfig.Rules.Add(new BaseLevelRule
            {
                CategoryName = "Стены",
                BaseMode = LevelChoiceMode.NearestBelow,
                TopMode = LevelChoiceMode.NearestAbove
            });
            defConfig.Rules.Add(new BaseLevelRule
            {
                CategoryName = "Несущие колонны",
                BaseMode = LevelChoiceMode.NearestBelow,
                TopMode = LevelChoiceMode.NearestAbove
            });
            defConfig.Rules.Add(new BaseLevelRule
            {
                CategoryName = "Трубы",
                BaseMode = LevelChoiceMode.NearestBelow,
                TopMode = LevelChoiceMode.Keep
            });
            defConfig.Rules.Add(new BaseLevelRule
            {
                CategoryName = "Воздуховоды",
                BaseMode = LevelChoiceMode.NearestBelow,
                TopMode = LevelChoiceMode.Keep
            });
            storage.Configurations.Add(defConfig);
            storage.SelectedConfigurationName = defConfig.Name;
            storage.Save();
            return storage;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                using (var ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(BaseLevelStorage));
                    serializer.WriteObject(ms, this);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(path, json, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
