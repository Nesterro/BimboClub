using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitServerManager.Services
{
    public static class RsnConfigService
    {
        public static List<string> GetCandidateRsnPaths(string version)
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            
            return new List<string>
            {
                Path.Combine(programData, "Autodesk", $"Revit Server {version}", "Config", "RSN.ini"),
                Path.Combine(programData, "Autodesk", $"Revit Server {version}", "Config", "rsn.ini"),
                Path.Combine(programData, "Autodesk", $"RevitServer{version}", "Config", "RSN.ini"),
                Path.Combine(programData, "Autodesk", $"RevitServer{version}", "Config", "rsn.ini"),
                Path.Combine(programData, "Autodesk", $"RVT {version}", "RSN.ini"),
                Path.Combine(programData, "Autodesk", $"RVT {version}", "rsn.ini"),
                Path.Combine(programData, "Autodesk", $"Revit {version}", "RSN.ini"),
                Path.Combine(programData, "Autodesk", $"Revit {version}", "rsn.ini"),
                Path.Combine(programData, "Autodesk", $"Revit {version}", "Config", "RSN.ini"),
                Path.Combine(programData, "Autodesk", $"Revit {version}", "Config", "rsn.ini")
            };
        }

        /// <summary>
        /// Поиск существующего файла RSN.ini для указанной версии Revit
        /// </summary>
        public static string? FindExistingRsnFile(string version)
        {
            foreach (var path in GetCandidateRsnPaths(version))
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Считывает список серверов из файлов RSN.ini для заданной версии Revit
        /// </summary>
        public static List<string> ReadServersForVersion(string version)
        {
            HashSet<string> servers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in GetCandidateRsnPaths(version))
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var lines = File.ReadAllLines(path);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;

                            // Удаляем возможные префиксы http:// или https://
                            string cleanHost = line.Replace("http://", "").Replace("https://", "").Trim('/');
                            if (!string.IsNullOrWhiteSpace(cleanHost))
                            {
                                servers.Add(cleanHost);
                            }
                        }
                    }
                    catch { }
                }
            }

            return servers.ToList();
        }

        /// <summary>
        /// Считывает все серверы из всех установленных версий RSN.ini (2019-2026)
        /// </summary>
        public static Dictionary<string, List<string>> ReadAllInstalledVersionsServers()
        {
            var result = new Dictionary<string, List<string>>();
            string[] versions = { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "2019" };

            foreach (var ver in versions)
            {
                var srvs = ReadServersForVersion(ver);
                if (srvs.Count > 0)
                {
                    result[ver] = srvs;
                }
            }

            return result;
        }
    }
}
