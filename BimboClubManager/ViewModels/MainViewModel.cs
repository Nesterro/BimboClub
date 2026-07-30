using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BimboClubManager.Models;
using BimboClubManager.Services;

namespace BimboClubManager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly RevitDetectorService _detectorService;
        private readonly UpdateService _updateService;
        
        private bool _isLoading;
        private double _globalProgress;
        private string _progressText = string.Empty;
        private string _updateSource = string.Empty;
        private bool _autoCloseRevit = true;
        private string _currentTab = "plugins"; // plugins, settings, about
        private string _changelog = "Нажмите 'Проверить обновления' для загрузки списка изменений.";
        private string _latestVersion = "—";
        private bool _showRevitWarning;
        private RevitVersionInfo? _pendingInstallVersion;

        public ObservableCollection<RevitVersionInfo> RevitVersions { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public double GlobalProgress
        {
            get => _globalProgress;
            set { _globalProgress = value; OnPropertyChanged(); }
        }

        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        public string UpdateSource
        {
            get => _updateSource;
            set { _updateSource = value; OnPropertyChanged(); }
        }

        public bool AutoCloseRevit
        {
            get => _autoCloseRevit;
            set { _autoCloseRevit = value; OnPropertyChanged(); }
        }

        public string CurrentTab
        {
            get => _currentTab;
            set { _currentTab = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPluginsTabActive)); OnPropertyChanged(nameof(IsSettingsTabActive)); OnPropertyChanged(nameof(IsAboutTabActive)); }
        }

        public bool IsPluginsTabActive => CurrentTab == "plugins";
        public bool IsSettingsTabActive => CurrentTab == "settings";
        public bool IsAboutTabActive => CurrentTab == "about";

        public string Changelog
        {
            get => _changelog;
            set { _changelog = value; OnPropertyChanged(); }
        }

        public string LatestVersion
        {
            get => _latestVersion;
            set { _latestVersion = value; OnPropertyChanged(); }
        }

        public bool ShowRevitWarning
        {
            get => _showRevitWarning;
            set { _showRevitWarning = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand NavigateCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand CloseWarningCommand { get; }
        public ICommand ForceInstallCommand { get; }

        public MainViewModel()
        {
            _detectorService = new RevitDetectorService();
            _updateService = new UpdateService();

            // Initialize Commands
            NavigateCommand = new RelayCommand<string>(tab => CurrentTab = tab ?? "plugins");
            CheckUpdatesCommand = new RelayCommand(async () => await CheckUpdatesAsync());
            InstallCommand = new RelayCommand<RevitVersionInfo>(async ver => await StartInstallAsync(ver));
            UninstallCommand = new RelayCommand<RevitVersionInfo>(ver => Uninstall(ver));
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            CloseWarningCommand = new RelayCommand(() => ShowRevitWarning = false);
            ForceInstallCommand = new RelayCommand(async () => await ForceInstallAsync());

            LoadSettings();
            
            // Initial scan
            if (OperatingSystem.IsWindows())
            {
                RefreshLocalVersions();
            }
        }

        [SupportedOSPlatform("windows")]
        private void RefreshLocalVersions()
        {
            var versions = _detectorService.DetectRevitInstallations();
            RevitVersions.Clear();
            foreach (var ver in versions)
            {
                RevitVersions.Add(ver);
            }
            EvaluateUpdates();
        }

        private async Task CheckUpdatesAsync()
        {
            IsLoading = true;
            ProgressText = "Проверка обновлений...";
            GlobalProgress = 10;

            try
            {
                var manifest = await _updateService.FetchManifestAsync(UpdateSource);
                GlobalProgress = 70;
                
                if (manifest != null)
                {
                    LatestVersion = manifest.LatestVersion;
                    Changelog = $"Версия: {manifest.LatestVersion} ({manifest.ReleaseDate})\n\nИзменения:\n" + 
                                string.Join("\n", manifest.Changelog);
                    
                    if (OperatingSystem.IsWindows())
                    {
                        RefreshLocalVersions();
                    }
                }
                else
                {
                    LatestVersion = "Неизвестно";
                    Changelog = "Ошибка проверки обновлений. Проверьте путь к источнику обновлений в настройках.";
                    MessageBox.Show("Не удалось получить манифест обновлений. Проверьте настройки источника.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Changelog = $"Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка проверки обновлений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GlobalProgress = 100;
                await Task.Delay(500);
                IsLoading = false;
                GlobalProgress = 0;
            }
        }

        private void EvaluateUpdates()
        {
            if (string.IsNullOrEmpty(LatestVersion) || LatestVersion == "—" || LatestVersion == "Неизвестно")
                return;

            try
            {
                var latestVer = new Version(LatestVersion);
                foreach (var ver in RevitVersions)
                {
                    if (!ver.IsRevitInstalled) continue;

                    if (ver.IsPluginInstalled)
                    {
                        var installedVer = new Version(ver.InstalledVersion);
                        if (latestVer > installedVer)
                        {
                            ver.IsUpdateAvailable = true;
                            ver.AvailableVersion = LatestVersion;
                            ver.StatusDescription = $"Доступно обновление до v{LatestVersion}";
                        }
                        else
                        {
                            ver.IsUpdateAvailable = false;
                            ver.AvailableVersion = "—";
                            ver.StatusDescription = $"Установлен (v{ver.InstalledVersion})";
                        }
                    }
                    else
                    {
                        ver.IsUpdateAvailable = false;
                        ver.AvailableVersion = LatestVersion;
                        ver.StatusDescription = "Не установлен";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error evaluating updates: {ex.Message}");
            }
        }

        private async Task StartInstallAsync(RevitVersionInfo? version)
        {
            if (version == null) return;

            // Check if Revit is running
            if (_updateService.IsRevitRunning())
            {
                if (AutoCloseRevit)
                {
                    _updateService.TerminateRevit();
                }
                else
                {
                    _pendingInstallVersion = version;
                    ShowRevitWarning = true;
                    return;
                }
            }

            await ProceedInstallAsync(version);
        }

        private async Task ForceInstallAsync()
        {
            ShowRevitWarning = false;
            if (_pendingInstallVersion == null) return;

            IsLoading = true;
            ProgressText = "Закрытие Revit...";
            await Task.Run(() => _updateService.TerminateRevit());
            
            await ProceedInstallAsync(_pendingInstallVersion);
            _pendingInstallVersion = null;
        }

        private async Task ProceedInstallAsync(RevitVersionInfo version)
        {
            IsLoading = true;
            ProgressText = $"Установка плагина для Revit {version.Year}...";
            GlobalProgress = 0;

            try
            {
                var manifest = await _updateService.FetchManifestAsync(UpdateSource);
                if (manifest == null)
                {
                    throw new Exception("Не удалось загрузить манифест обновлений перед установкой.");
                }

                var progressReporter = new Progress<double>(val => {
                    GlobalProgress = val;
                    if (val < 40) ProgressText = "Загрузка пакета...";
                    else if (val < 60) ProgressText = "Распаковка...";
                    else if (val < 95) ProgressText = "Копирование файлов...";
                    else ProgressText = "Завершение установки...";
                });

                await Task.Run(async () => {
                    await _updateService.InstallUpdateAsync(
                        version, 
                        manifest, 
                        UpdateSource, 
                        progressReporter, 
                        CancellationToken.None);
                });

                if (OperatingSystem.IsWindows())
                {
                    RefreshLocalVersions();
                }
                MessageBox.Show($"Плагин успешно установлен для Revit {version.Year}!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка установки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                GlobalProgress = 0;
            }
        }

        private void Uninstall(RevitVersionInfo? version)
        {
            if (version == null) return;

            if (_updateService.IsRevitRunning())
            {
                var res = MessageBox.Show(
                    "Revit запущен. Пожалуйста, закройте Revit перед удалением, иначе файлы будут заблокированы. Закрыть Revit автоматически?", 
                    "Внимание", 
                    MessageBoxButton.YesNoCancel, 
                    MessageBoxImage.Warning);
                
                if (res == MessageBoxResult.Yes)
                {
                    _updateService.TerminateRevit();
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            try
            {
                _updateService.UninstallPlugin(version);
                if (OperatingSystem.IsWindows())
                {
                    RefreshLocalVersions();
                }
                MessageBox.Show($"Плагин удален для Revit {version.Year}.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Settings load & save
        private class UserSettings
        {
            public string UpdateSource { get; set; } = string.Empty;
            public bool AutoCloseRevit { get; set; } = true;
        }

        private void LoadSettings()
        {
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimboClubManager");
            string configPath = Path.Combine(configDir, "config.json");

            // Default GitHub raw update manifest URL
            string defaultMockPath = "https://raw.githubusercontent.com/Nesterro/BimboClub/main/updates/update_manifest.json";

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    if (settings != null)
                    {
                        UpdateSource = string.IsNullOrEmpty(settings.UpdateSource) ? defaultMockPath : settings.UpdateSource;
                        AutoCloseRevit = settings.AutoCloseRevit;
                        return;
                    }
                }
                catch
                {
                    // Ignore, fallback to defaults
                }
            }

            // Default fallbacks
            UpdateSource = defaultMockPath;
            AutoCloseRevit = true;
        }

        private void SaveSettings()
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimboClubManager");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                string configPath = Path.Combine(configDir, "config.json");

                var settings = new UserSettings
                {
                    UpdateSource = UpdateSource,
                    AutoCloseRevit = AutoCloseRevit
                };

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                MessageBox.Show("Настройки сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // If settings saved, re-evaluate updates
                if (OperatingSystem.IsWindows())
                {
                    RefreshLocalVersions();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Helper Command Classes
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter == null && typeof(T).IsValueType) return _canExecute?.Invoke(default) ?? true;
            return _canExecute?.Invoke((T?)parameter) ?? true;
        }

        public void Execute(object? parameter) => _execute((T?)parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
