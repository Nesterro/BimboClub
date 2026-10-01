using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using BimboClubManager.Models;

namespace BimboClubManager.Services
{
    public class RevitDetectorService
    {
        private static readonly string[] SupportedYears = { "2021", "2022", "2023", "2024", "2025", "2026" };

        [SupportedOSPlatform("windows")]
        public List<RevitVersionInfo> DetectRevitInstallations()
        {
            var versions = new List<RevitVersionInfo>();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            foreach (var year in SupportedYears)
            {
                var info = new RevitVersionInfo
                {
                    Year = year,
                    TargetFramework = int.Parse(year) <= 2024 ? "net48" : "net8.0-windows"
                };

                // Detect Revit installation
                string installPath = GetRevitInstallPathFromRegistry(year);
                if (string.IsNullOrEmpty(installPath))
                {
                    string standardPath = $@"C:\Program Files\Autodesk\Revit {year}";
                    if (Directory.Exists(standardPath))
                    {
                        installPath = standardPath;
                    }
                }

                if (!string.IsNullOrEmpty(installPath))
                {
                    info.IsRevitInstalled = true;
                    info.RevitInstallPath = installPath;
                }
                else
                {
                    info.IsRevitInstalled = false;
                    info.StatusDescription = "Revit не установлен";
                }

                // Check possible plugin paths:
                // 1) Current user AppData
                // 2) All users ProgramData
                string appDataAddin = Path.Combine(appData, "Autodesk", "Revit", "Addins", year, "BimboClub.addin");
                string appDataDll = Path.Combine(appData, "Autodesk", "Revit", "Addins", year, "BimboClub.dll");

                string progDataAddin = Path.Combine(programData, "Autodesk", "Revit", "Addins", year, "BimboClub.addin");
                string progDataDll = Path.Combine(programData, "Autodesk", "Revit", "Addins", year, "BimboClub.dll");

                bool appDataExists = File.Exists(appDataAddin) && File.Exists(appDataDll);
                bool progDataExists = File.Exists(progDataAddin) && File.Exists(progDataDll);

                if (appDataExists)
                {
                    info.AddinPath = appDataAddin;
                    info.DllPath = appDataDll;
                }
                else if (progDataExists)
                {
                    info.AddinPath = progDataAddin;
                    info.DllPath = progDataDll;
                }
                else
                {
                    // Default to AppData for new installs
                    info.AddinPath = appDataAddin;
                    info.DllPath = appDataDll;
                }

                // Detect Plugin installation
                if (info.IsRevitInstalled)
                {
                    if (appDataExists || progDataExists)
                    {
                        info.IsPluginInstalled = true;
                        info.InstalledVersion = GetDllVersion(info.DllPath);
                        info.StatusDescription = $"Установлен (v{info.InstalledVersion})";
                    }
                    else
                    {
                        info.IsPluginInstalled = false;
                        info.InstalledVersion = "—";
                        info.StatusDescription = "Не установлен";
                    }
                }

                versions.Add(info);
            }

            return versions;
        }

        [SupportedOSPlatform("windows")]
        private string GetRevitInstallPathFromRegistry(string year)
        {
            try
            {
                string keyPath = $@"SOFTWARE\Autodesk\Revit\{year}";
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(keyPath);
                
                if (key != null)
                {
                    var installPath = key.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                    {
                        return installPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking registry for Revit {year}: {ex.Message}");
            }
            return string.Empty;
        }

        public string GetDllVersion(string dllPath)
        {
            try
            {
                if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
                {
                    return "—";
                }

                // 1. Check version.txt in same directory
                string? dir = Path.GetDirectoryName(dllPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string verFile = Path.Combine(dir, "version.txt");
                    if (File.Exists(verFile))
                    {
                        string txtVer = File.ReadAllText(verFile).Trim().TrimStart('v', 'V');
                        if (IsValidVersion(txtVer))
                        {
                            return txtVer;
                        }
                    }
                }

                // 2. Read FileVersionInfo
                var versionInfo = FileVersionInfo.GetVersionInfo(dllPath);
                string? version = versionInfo.ProductVersion;
                if (!string.IsNullOrEmpty(version))
                {
                    version = CleanVersionString(version);
                    if (IsValidVersion(version))
                    {
                        return version;
                    }
                }

                version = versionInfo.FileVersion;
                if (!string.IsNullOrEmpty(version))
                {
                    version = CleanVersionString(version);
                    if (IsValidVersion(version))
                    {
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading DLL version from {dllPath}: {ex.Message}");
            }

            return "2.4.20";
        }

        private static string CleanVersionString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string v = raw.Trim().TrimStart('v', 'V');
            int plusIdx = v.IndexOf('+');
            if (plusIdx >= 0) v = v.Substring(0, plusIdx);
            int spaceIdx = v.IndexOf(' ');
            if (spaceIdx >= 0) v = v.Substring(0, spaceIdx);
            return v.Trim();
        }

        private static bool IsValidVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            if (v == "1.0.0" || v == "1.0.0.0") return false;
            return Version.TryParse(v, out _);
        }
    }
}
