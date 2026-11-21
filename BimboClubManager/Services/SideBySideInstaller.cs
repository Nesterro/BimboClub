using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BimboClub.Installation;

namespace BimboClubManager.Services
{
    internal static class SideBySideInstaller
    {
        internal static string Install(string source, string manifest, string version, string framework, CancellationToken cancellationToken)
        {
            if (!Version.TryParse(version.TrimStart('v', 'V'), out var expected) || expected.Build < 0)
                throw new InvalidDataException("Неверный номер версии пакета.");
            string sourceAssembly = Path.Combine(source, PluginInstallation.PayloadName);
            var assembly = AssemblyName.GetAssemblyName(sourceAssembly);
            if (assembly.Version == null || assembly.Version.Major != expected.Major || assembly.Version.Minor != expected.Minor || assembly.Version.Build != expected.Build ||
                (assembly.Name != "BimboClub" && assembly.Name != PluginInstallation.LiveAssemblyName(expected)))
                throw new InvalidDataException("Версия или имя сборки не совпадают с манифестом обновления.");
            ValidateFramework(sourceAssembly, framework);

            string root = Path.Combine(Path.GetDirectoryName(manifest)!, PluginInstallation.PackageDirectory);
            Directory.CreateDirectory(root);
            using var installLock = new FileStream(Path.Combine(root, ".install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            string directory = Path.Combine(root, $"{expected}-{Guid.NewGuid():N}");
            string payload = Path.Combine(directory, PluginInstallation.PayloadName);
            string manifestTemp = manifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Path.GetExtension(file).Equals(".addin", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = Path.Combine(directory, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
                }
                File.WriteAllText(Path.Combine(directory, "version.txt"), expected.ToString());
                PluginInstallation.Registration(payload, PluginInstallation.StartupId).Save(manifestTemp);
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(manifest)) File.Replace(manifestTemp, manifest, null);
                else File.Move(manifestTemp, manifest);
                return payload;
            }
            catch
            {
                try { File.Delete(manifestTemp); if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        private static void ValidateFramework(string assemblyPath, string framework)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            bool hasApp = metadata.TypeDefinitions.Any(handle =>
            {
                var type = metadata.GetTypeDefinition(handle);
                return metadata.GetString(type.Namespace) == "BimboClub" && metadata.GetString(type.Name) == "App";
            });
            if (!hasApp) throw new InvalidDataException("В пакете нет точки входа BimboClub.App.");
            string target = framework == "net48" ? ".NETFramework,Version=v4.8" : ".NETCoreApp,Version=v8.0";
            foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = metadata.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
                var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
                var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                if (metadata.GetString(type.Namespace) != "System.Runtime.Versioning" || metadata.GetString(type.Name) != "TargetFrameworkAttribute") continue;
                var blob = metadata.GetBlobReader(attribute.Value);
                if (blob.ReadUInt16() == 1 && blob.ReadSerializedString() == target) return;
                break;
            }
            throw new InvalidDataException("Пакет собран для другой платформы Revit.");
        }
    }
}
