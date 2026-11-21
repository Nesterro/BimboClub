#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace BimboClub.Installation
{
    internal static class PluginInstallation
    {
        internal const string ManifestName = "BimboClub.addin";
        internal const string PackageDirectory = "BimboClub.Packages";
        internal const string PayloadName = "BimboClub.dll";
        internal static readonly Guid StartupId = new Guid("B13B0C1B-C10B-4DDE-A3C9-968B750A8610");

        internal static string LiveAssemblyName(Version version) =>
            $"BimboClub.Tools.V{version.Major}_{version.Minor}_{version.Build}";

        internal static string? ReadPayload(string manifest)
        {
            try
            {
                using (var file = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var registration = XDocument.Load(file).Root?.Elements("AddIn").SingleOrDefault();
                    if (registration == null || !Guid.TryParse((string?)registration.Element("AddInId"), out var id) || id != StartupId) return null;
                    if ((string?)registration.Element("FullClassName") != "BimboClub.App") return null;
                    var path = (string?)registration.Element("Assembly");
                    return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest) ?? "", path));
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
            catch (System.Xml.XmlException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        internal static XDocument Registration(string payload, Guid id) => new XDocument(
            new XElement("RevitAddIns", new XElement("AddIn", new XAttribute("Type", "Application"),
                new XElement("Name", "BimboClub Tools"), new XElement("Assembly", payload),
                new XElement("AddInId", id), new XElement("FullClassName", "BimboClub.App"),
                new XElement("VendorId", "BMBC"), new XElement("VendorDescription", "BimboClub"),
                new XElement("AllowLoadingIntoExistingSession", id == StartupId ? "No" : "Yes"))));

        internal static bool IsManagedPayload(string manifest, string? payload)
        {
            if (payload == null || !string.Equals(Path.GetFileName(payload), PayloadName, StringComparison.OrdinalIgnoreCase)) return false;
            string? generation = Path.GetDirectoryName(payload);
            return string.Equals(Path.GetDirectoryName(generation),
                Path.Combine(Path.GetDirectoryName(manifest) ?? "", PackageDirectory), StringComparison.OrdinalIgnoreCase)
                && IsGeneration(Path.GetFileName(generation));
        }

        internal static bool IsGeneration(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int separator = name.IndexOf('-');
            return separator > 0 && Version.TryParse(name.Substring(0, separator), out _)
                && Guid.TryParseExact(name.Substring(separator + 1), "N", out _);
        }

        internal static bool IsLiveCandidate(AssemblyName current, AssemblyName candidate) =>
            candidate.Version != null && current.Version != null && candidate.Version > current.Version &&
            string.Equals(candidate.Name, LiveAssemblyName(candidate.Version), StringComparison.Ordinal) &&
            !string.Equals(candidate.Name, current.Name, StringComparison.OrdinalIgnoreCase);
    }
}
