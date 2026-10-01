using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class InfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new InfoWindow(commandData.Application);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        public static string GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // 1. Try InformationalVersion attribute (e.g. "2.4.20" or "2.4.20+commit")
                var infoVerAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoVerAttr != null && !string.IsNullOrWhiteSpace(infoVerAttr.InformationalVersion))
                {
                    string v = CleanVersionString(infoVerAttr.InformationalVersion);
                    if (IsValidVersion(v)) return v;
                }

                // 2. Try FileVersionInfo ProductVersion / FileVersion
                if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                {
                    var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                    if (!string.IsNullOrWhiteSpace(fvi.ProductVersion))
                    {
                        string v = CleanVersionString(fvi.ProductVersion);
                        if (IsValidVersion(v)) return v;
                    }

                    if (!string.IsNullOrWhiteSpace(fvi.FileVersion))
                    {
                        string v = CleanVersionString(fvi.FileVersion);
                        if (IsValidVersion(v)) return v;
                    }

                    // 3. Try reading version.txt in the same directory (written by manager or installer)
                    string dir = Path.GetDirectoryName(assembly.Location);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        string verFile = Path.Combine(dir, "version.txt");
                        if (File.Exists(verFile))
                        {
                            string v = CleanVersionString(File.ReadAllText(verFile));
                            if (IsValidVersion(v)) return v;
                        }
                    }
                }

                // 4. Try Assembly Name Version
                var ver = assembly.GetName().Version;
                if (ver != null && !(ver.Major == 1 && ver.Minor == 0 && ver.Build == 0))
                {
                    return $"{ver.Major}.{ver.Minor}.{ver.Build}";
                }
            }
            catch
            {
                // Fallback
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

