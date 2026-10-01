using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimboClubManager.Models
{
    public class RevitVersionInfo : INotifyPropertyChanged
    {
        private string _installedVersion = "—";
        private string _availableVersion = "—";
        private bool _isRevitInstalled;
        private bool _isPluginInstalled;
        private bool _isUpdateAvailable;
        private string _statusDescription = "Не обнаружен";

        public string Year { get; set; } = string.Empty;
        public string DisplayName => $"Autodesk Revit {Year}";
        public string TargetFramework { get; set; } = "net48"; // net48 or net8.0-windows
        public string RevitInstallPath { get; set; } = string.Empty;
        public string AddinPath { get; set; } = string.Empty;
        public string DllPath { get; set; } = string.Empty;

        public bool IsRevitInstalled
        {
            get => _isRevitInstalled;
            set
            {
                if (_isRevitInstalled != value)
                {
                    _isRevitInstalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstalledVersionDisplay));
                    OnPropertyChanged(nameof(AvailableVersionDisplay));
                    OnPropertyChanged(nameof(IsAvailableVersionUpToDate));
                }
            }
        }

        public bool IsPluginInstalled
        {
            get => _isPluginInstalled;
            set
            {
                if (_isPluginInstalled != value)
                {
                    _isPluginInstalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstalledVersionDisplay));
                    OnPropertyChanged(nameof(AvailableVersionDisplay));
                    OnPropertyChanged(nameof(IsAvailableVersionUpToDate));
                }
            }
        }

        public string InstalledVersion
        {
            get => _installedVersion;
            set
            {
                if (_installedVersion != value)
                {
                    _installedVersion = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstalledVersionDisplay));
                    OnPropertyChanged(nameof(AvailableVersionDisplay));
                    OnPropertyChanged(nameof(IsAvailableVersionUpToDate));
                }
            }
        }

        public string AvailableVersion
        {
            get => _availableVersion;
            set
            {
                if (_availableVersion != value)
                {
                    _availableVersion = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableVersionDisplay));
                    OnPropertyChanged(nameof(IsAvailableVersionUpToDate));
                }
            }
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set
            {
                if (_isUpdateAvailable != value)
                {
                    _isUpdateAvailable = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableVersionDisplay));
                    OnPropertyChanged(nameof(IsAvailableVersionUpToDate));
                }
            }
        }

        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                if (_statusDescription != value)
                {
                    _statusDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InstalledVersionDisplay
        {
            get
            {
                if (!IsRevitInstalled) return "Revit не установлен";
                if (!IsPluginInstalled || string.IsNullOrWhiteSpace(InstalledVersion) || InstalledVersion == "—") return "Не установлен";
                return InstalledVersion.StartsWith("v", System.StringComparison.OrdinalIgnoreCase) ? InstalledVersion : $"v{InstalledVersion}";
            }
        }

        public string AvailableVersionDisplay
        {
            get
            {
                if (!IsRevitInstalled) return "—";
                if (IsPluginInstalled && !IsUpdateAvailable && !string.IsNullOrWhiteSpace(InstalledVersion) && InstalledVersion != "—")
                {
                    return "Актуально";
                }
                if (string.IsNullOrWhiteSpace(AvailableVersion) || AvailableVersion == "—") return "—";
                return AvailableVersion.StartsWith("v", System.StringComparison.OrdinalIgnoreCase) ? AvailableVersion : $"v{AvailableVersion}";
            }
        }

        public bool IsAvailableVersionUpToDate => IsRevitInstalled && IsPluginInstalled && !IsUpdateAvailable;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
