using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BimboClub.Installation;

namespace BimboClub
{
    internal sealed class LiveRibbonUpdate
    {
        private const string CandidateKey = "BimboClub.Tools.LiveCandidate";
        private readonly UIControlledApplication _application;
        private readonly List<RibbonPanel> _panels = new List<RibbonPanel>();
        private readonly string _payload;
        private readonly string _manifest;
        private readonly string _packageRoot;
        private readonly string _panelSuffix = Guid.NewGuid().ToString("N");
        private readonly bool _late;
        private AssemblyName _activeIdentity;
        private string _activePayload;
        private Action<bool> _setActiveVisible;
        private bool _subscribed;
        private DateTime _nextCheck;
        private string _attempted;
        private FileStream _lease;

        internal LiveRibbonUpdate(UIControlledApplication application, string payload, AssemblyName identity)
        {
            _application = application;
            _payload = Path.GetFullPath(payload);
            _activeIdentity = identity;
            _activePayload = _payload;
            _setActiveVisible = SetVisible;
            string directory = Path.GetDirectoryName(_payload);
            string parent = Path.GetDirectoryName(directory);
            bool managed = PluginInstallation.IsGeneration(Path.GetFileName(directory))
                && Path.GetFileName(parent) == PluginInstallation.PackageDirectory;
            _packageRoot = managed ? parent : null;
            _manifest = Path.Combine(managed ? Path.GetDirectoryName(parent) : directory, PluginInstallation.ManifestName);
            _late = string.Equals(AppDomain.CurrentDomain.GetData(CandidateKey) as string, _payload, StringComparison.OrdinalIgnoreCase);
            if (managed)
            {
                try { _lease = new FileStream(Path.Combine(directory, ".session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite); }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal RibbonPanel CreatePanel(string tab, string title)
        {
            var panel = _application.CreateRibbonPanel(tab, "BCC_" + _panelSuffix + "_" + _panels.Count);
            _panels.Add(panel);
            panel.Title = title;
            if (_late) panel.Visible = false;
            return panel;
        }

        internal void Start()
        {
            if (_late)
            {
                // LoadAddIn runs inside the startup generation's Idling callback.
                // Revit forbids changing the event subscription while it is executing.
                AppDomain.CurrentDomain.SetData(CandidateKey, (Action<bool>)SetVisible);
                return;
            }
            _application.Idling += OnIdling;
            _subscribed = true;
            if (_packageRoot != null) CleanRetiredPackages();
        }

        internal void Stop()
        {
            if (_subscribed)
            {
                _application.Idling -= OnIdling;
                _subscribed = false;
            }
            _lease?.Dispose();
            _lease = null;
        }

        private void SetVisible(bool visible)
        {
            foreach (var panel in _panels) panel.Visible = visible;
        }

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            var now = DateTime.UtcNow;
            if (_nextCheck > now) return;
            _nextCheck = now.AddSeconds(5);
            string candidate = PluginInstallation.ReadPayload(_manifest);
            if (!PluginInstallation.IsManagedPayload(_manifest, candidate) ||
                string.Equals(candidate, _activePayload, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, _attempted, StringComparison.OrdinalIgnoreCase)) return;

            _attempted = candidate;
            string registration = null;
            Action<bool> switchCandidate = null;
            try
            {
                var candidateIdentity = AssemblyName.GetAssemblyName(candidate);
                if (!PluginInstallation.IsLiveCandidate(_activeIdentity, candidateIdentity) ||
                    (File.GetAttributes(Path.GetDirectoryName(candidate)) & FileAttributes.ReparsePoint) != 0)
                    return;
                // Keeping the temporary manifest beside the payload prevents startup discovery.
                registration = Path.Combine(Path.GetDirectoryName(candidate), $"session-{Guid.NewGuid():N}.addin");
                PluginInstallation.Registration(candidate, Guid.NewGuid()).Save(registration);
                AppDomain.CurrentDomain.SetData(CandidateKey, candidate);
                _application.LoadAddIn(registration);
                switchCandidate = AppDomain.CurrentDomain.GetData(CandidateKey) as Action<bool>;
                if (switchCandidate == null) throw new InvalidOperationException("Не подтверждена готовность новой панели BimboClub.");
                switchCandidate(true);
                _setActiveVisible(false);
                _setActiveVisible = switchCandidate;
                _activeIdentity = candidateIdentity;
                _activePayload = candidate;
            }
            catch (Exception ex)
            {
                try
                {
                    switchCandidate?.Invoke(false);
                    _setActiveVisible(true);
                }
                catch (Exception restoreError) { Logger.LogError("Восстановление видимости ленты", restoreError); }
                Logger.LogError("Обновление в открытом Revit. Версия установлена для следующего запуска", ex);
            }
            finally
            {
                AppDomain.CurrentDomain.SetData(CandidateKey, null);
                if (registration != null)
                {
                    try { File.Delete(registration); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private void CleanRetiredPackages()
        {
            // Never race another Revit instance that may be opening a previous registration.
            var processes = Process.GetProcessesByName("Revit");
            int count = processes.Length;
            foreach (var process in processes) process.Dispose();
            if (count != 1) return;
            try
            {
                using (var installLock = new FileStream(Path.Combine(_packageRoot, ".install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    string active = PluginInstallation.ReadPayload(_manifest);
                    if (!string.Equals(active, _payload, StringComparison.OrdinalIgnoreCase)) return;
                    string rootPrefix = Path.GetFullPath(_packageRoot) + Path.DirectorySeparatorChar;
                    foreach (string directory in Directory.EnumerateDirectories(_packageRoot))
                    {
                        if (!Path.GetFullPath(directory).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                            !PluginInstallation.IsGeneration(Path.GetFileName(directory)) ||
                            string.Equals(directory, Path.GetDirectoryName(_payload), StringComparison.OrdinalIgnoreCase) ||
                            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                        try
                        {
                            string leasePath = Path.Combine(directory, ".session.lock");
                            using (var lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { }
                            Directory.Delete(directory, recursive: true);
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
