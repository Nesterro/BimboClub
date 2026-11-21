using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Autodesk.Revit.UI;
using BimboClub;
using BimboClub.Installation;
using BimboClubManager.Services;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

void Reject<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException(message);
}

string preview = Path.GetFullPath(args[0]);
string folder = Path.Combine(Path.GetTempPath(), $"BimboClub-portfolio-{Guid.NewGuid():N}");
Directory.CreateDirectory(folder);
try
{
    string manifest = Path.Combine(folder, PluginInstallation.ManifestName);
    string legacy = Path.Combine(folder, PluginInstallation.PayloadName);
    File.WriteAllText(legacy, "legacy-binary");
    PluginInstallation.Registration(legacy, PluginInstallation.StartupId).Save(manifest);
    Check(PluginInstallation.ReadPayload(manifest) == legacy, "Legacy registration is resolved");

    string Extract(string version, string framework)
    {
        string source = Path.Combine(folder, $"input-{version}-{framework}");
        ZipFile.ExtractToDirectory(Path.Combine(preview, version, $"bimboclub_{framework}.zip"), source);
        return source;
    }

    string source1 = Extract("3.0.0", "net8");
    string source2 = Extract("3.0.1", "net8");
    string source48 = Extract("3.0.1", "net48");
    string first = SideBySideInstaller.Install(source1, manifest, "3.0.0", "net8.0-windows", CancellationToken.None);
    Check(PluginInstallation.ReadPayload(manifest) == first, "New installation is selected at startup");
    Check(File.ReadAllText(legacy) == "legacy-binary", "Legacy files stay untouched");
    Check(PluginInstallation.IsManagedPayload(manifest, first), "Installation uses a dedicated generation directory");
    Check((string)XDocument.Load(manifest).Root.Element("AddIn").Element("AllowLoadingIntoExistingSession") == "No",
        "The startup registration cannot race live activation");

    Reject<InvalidDataException>(() => SideBySideInstaller.Install(source48, manifest, "3.0.1", "net8", CancellationToken.None),
        "Wrong target platform was accepted");
    Reject<InvalidDataException>(() => SideBySideInstaller.Install(source2, manifest, "3.1.1", "net8", CancellationToken.None),
        "Wrong version was accepted");
    Reject<OperationCanceledException>(() => SideBySideInstaller.Install(source2, manifest, "3.0.1", "net8", new CancellationToken(true)),
        "Cancelled publication unexpectedly succeeded");
    Check(PluginInstallation.ReadPayload(manifest) == first, "Failed installs leave the manifest unchanged");
    string lockPath = Path.Combine(folder, PluginInstallation.PackageDirectory, ".install.lock");
    using (File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        Reject<IOException>(() => SideBySideInstaller.Install(source2, manifest, "3.0.1", "net8", CancellationToken.None),
            "Concurrent writer unexpectedly succeeded");

    using var running = File.Open(first, FileMode.Open, FileAccess.Read, FileShare.Read);
    string second = SideBySideInstaller.Install(source2, manifest, "3.0.1", "net8", CancellationToken.None);
    Check(PluginInstallation.ReadPayload(manifest) == second && first != second && File.Exists(first), "A running generation remains available");
    var current = AssemblyName.GetAssemblyName(first);
    var candidate = AssemblyName.GetAssemblyName(second);
    Check(PluginInstallation.IsLiveCandidate(current, candidate), "Real package binaries have distinct live identities");
    Check(!PluginInstallation.IsLiveCandidate(candidate, current), "Downgrades wait for startup");
    Check(!PluginInstallation.IsLiveCandidate(current, new AssemblyName("BimboClub, Version=4.0.0.0")), "Legacy payloads wait for startup");

    var application = new UIControlledApplication();
    var old = new LiveRibbonUpdate(application, first, current);
    var oldPanel = old.CreatePanel("BimboClub", "Old panel");
    RibbonPanel newPanel = null;
    LiveRibbonUpdate loadedCandidate = null;
    string temporaryManifest = null;
    application.OnLoad = path =>
    {
        temporaryManifest = path;
        var registration = XDocument.Load(path).Root.Element("AddIn");
        Check((string)registration.Element("Assembly") == second && oldPanel.Visible, "Old panel stays visible during loading");
        Check(Guid.Parse((string)registration.Element("AddInId")) != PluginInstallation.StartupId, "Live registration has a different AddInId");
        loadedCandidate = new LiveRibbonUpdate(application, second, candidate);
        newPanel = loadedCandidate.CreatePanel("BimboClub", "New panel");
        Check(!newPanel.Visible, "Candidate is initially hidden");
        loadedCandidate.Start();
    };
    old.Start();
    application.RaiseIdling();
    Check(newPanel != null && newPanel.Visible && !oldPanel.Visible, "Confirmed candidate replaces the old panel");
    Check(!File.Exists(temporaryManifest), "Temporary registration is removed");
    loadedCandidate.Stop();
    old.Stop();

    var failingApplication = new UIControlledApplication();
    var retry = new LiveRibbonUpdate(failingApplication, first, current);
    var retryPanel = retry.CreatePanel("BimboClub", "Original panel");
    int attempts = 0;
    failingApplication.OnLoad = path => { attempts++; throw new IOException("Simulated Revit load failure"); };
    retry.Start();
    failingApplication.RaiseIdling();
    failingApplication.RaiseIdling();
    Check(retryPanel.Visible && attempts == 1, "Load failure leaves the original panel usable");
    Check(PluginInstallation.ReadPayload(manifest) == second, "Staged package remains for restart");
    retry.Stop();
    Console.WriteLine($"PASS {checks} installation and ribbon handoff checks");
}
finally
{
    Directory.Delete(folder, recursive: true);
}
