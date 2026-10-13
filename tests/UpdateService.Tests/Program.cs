using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using BimboClubManager.Models;
using BimboClubManager.Services;

string sandbox = Path.Combine(Path.GetTempPath(), "BimboClubTests-" + Guid.NewGuid());
Directory.CreateDirectory(sandbox);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
try
{
    string source = Path.Combine(sandbox, "source");
    Directory.CreateDirectory(source);
    string manifestPath = Path.Combine(source, "update_manifest.json");
    File.WriteAllText(manifestPath, "{}");
    string payload = Path.Combine(sandbox, "payload");
    Directory.CreateDirectory(payload);
    string binary = Assembly.GetExecutingAssembly().Location;
    string version = FileVersionInfo.GetVersionInfo(binary).FileVersion!;
    File.Copy(binary, Path.Combine(payload, "BimboClub.dll"));
    string zip = Path.Combine(source, "bimboclub_net8.zip");
    ZipFile.CreateFromDirectory(payload, zip);

    Check(UpdateService.FindLocalPackage(manifestPath, "https://example.invalid/bimboclub_net8.zip") == null,
        "A remote release must never resolve to a same-named local ZIP.");
    Check(UpdateService.FindLocalPackage(manifestPath, "bimboclub_net8.zip") == zip,
        "Relative packages must resolve against the manifest directory.");
    Check(UpdateService.FindLocalPackage(source, "bimboclub_net8.zip") == zip,
        "Directory sources must support relative packages.");
    UpdateService.ValidatePackage(payload, version);
    checks++;
    try { UpdateService.ValidatePackage(payload, "99.0.0"); throw new Exception("Mismatched version accepted"); }
    catch (InvalidDataException) { checks++; }
    string empty = Path.Combine(sandbox, "empty");
    Directory.CreateDirectory(empty);
    try { UpdateService.ValidatePackage(empty, version); throw new Exception("Empty package accepted"); }
    catch (InvalidDataException) { checks++; }

    var manifest = new UpdateService.UpdateManifest {
        LatestVersion = version,
        Packages = new UpdateService.PackageInfo { Net8Url = "bimboclub_net8.zip" }
    };
    string target = Path.Combine(sandbox, "installed");
    var info = new RevitVersionInfo { Year = "2025", TargetFramework = "net8.0-windows",
        AddinPath = Path.Combine(target, "BimboClub.addin"), DllPath = Path.Combine(target, "BimboClub.dll") };
    await new UpdateService().InstallUpdateAsync(info, manifest, manifestPath, new Progress<double>(), CancellationToken.None);
    Check(File.Exists(info.DllPath) && File.Exists(info.AddinPath), "Installation must include DLL and addin manifest.");
    Check(File.ReadAllText(Path.Combine(target, "version.txt")) == version, "Version must reflect installed payload.");
    File.WriteAllText(Path.Combine(target, "version.txt"), "0.0.1");
    Check(new RevitDetectorService().GetDllVersion(info.DllPath) != "0.0.1", "Stale version.txt must not override binary metadata.");
    manifest.LatestVersion = "99.0.0";
    byte[] original = File.ReadAllBytes(info.DllPath);
    try { await new UpdateService().InstallUpdateAsync(info, manifest, manifestPath, new Progress<double>(), CancellationToken.None); throw new Exception("Bad install accepted"); }
    catch (InvalidDataException) { checks++; }
    Check(original.SequenceEqual(File.ReadAllBytes(info.DllPath)), "Rejected package must not change installation.");
    Check(File.ReadAllText(Path.Combine(target, "version.txt")) == "0.0.1", "Rejected package must not change version marker.");
    Console.WriteLine($"PASS: {checks} update regression checks.");
}
finally { Directory.Delete(sandbox, true); }
