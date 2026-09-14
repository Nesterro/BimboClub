using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RevitServerManager.Models
{
    public class AppSettings
    {
        public string ServerAddress { get; set; } = "127.0.0.1";
        public string ServerVersion { get; set; } = "2024";
        public List<string> RecentServers { get; set; } = new() { "127.0.0.1" };
        public string DestinationFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RevitServer_Models");
        public string CustomRevitServerToolPath { get; set; } = "";
        public bool OverwriteExisting { get; set; } = true;
        public bool PreserveSubfolders { get; set; } = true;
        public bool OpenFolderOnComplete { get; set; } = true;

        private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimboClub", "RevitServerManager");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                if (!string.IsNullOrWhiteSpace(ServerAddress) && !RecentServers.Contains(ServerAddress))
                {
                    RecentServers.Insert(0, ServerAddress);
                    if (RecentServers.Count > 10) RecentServers.RemoveAt(RecentServers.Count - 1);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
