using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private bool _autoCloseRevit = false; // Never silently kill Revit by default
        private string _currentTab = "plugins"; // plugins, settings, about
        private string _changelog = "Загрузка списка изменений...";
        private string _latestVersion = "—";
        private bool _showRevitWarning;
        private string _revitWarningStatus = string.Empty;
        private RevitVersionInfo? _pendingInstallVersion;
        private bool _pendingInstallAll;

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
            set { _updateSource = value; OnPropertyChanged(); OnPropertyChanged(nameof(GitHubRepo)); }
        }

        public string GitHubRepo
        {
            get => UpdateSource;
            set { UpdateSource = value; OnPropertyChanged(); }
        }

        public bool AutoCloseRevit
        {
            get => _autoCloseRevit;
            set { _autoCloseRevit = value; OnPropertyChanged(); }
        }

        public bool AutoCheckUpdates { get; set; } = true;
        public bool IncludePreReleases { get; set; } = false;

        public string CurrentTab
        {
            get => _currentTab;
            set 
            { 
                _currentTab = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsPluginsTabActive)); 
                OnPropertyChanged(nameof(IsSettingsTabActive)); 
                OnPropertyChanged(nameof(IsAboutTabActive)); 
            }
        }

        public bool IsPluginsTabActive => CurrentTab == "plugins";
        public bool IsSettingsTabActive => CurrentTab == "settings";
        public bool IsAboutTabActive => CurrentTab == "about";

        public string Changelog
        {
            get => _changelog;
            set 
            { 
                _changelog = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(LatestReleaseBody));
            }
        }

        public string LatestVersion
        {
            get => _latestVersion;
            set 
            { 
                _latestVersion = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(LatestReleaseVersion));
                OnPropertyChanged(nameof(LatestReleaseName));
            }
        }

        public string ClientVersion
        {
            get
            {
                try
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    var ver = asm.GetName().Version;
                    return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v2.4.21";
                }
                catch
                {
                    return "v2.4.21";
                }
            }
        }

        // Aliases for WPF Binding Compatibility
        public string LatestReleaseVersion => LatestVersion;
        public string LatestReleaseName => string.IsNullOrEmpty(LatestVersion) || LatestVersion == "—" ? "Список изменений" : $"Версия {LatestVersion}";
        public string LatestReleaseBody => Changelog;

        public bool ShowRevitWarning
        {
            get => _showRevitWarning;
            set { _showRevitWarning = value; OnPropertyChanged(); }
        }

        public string RevitWarningStatus
        {
            get => _revitWarningStatus;
            set { _revitWarningStatus = value; OnPropertyChanged(); }
        }

        public bool HasUpdatesAvailable => RevitVersions.Any(v => v.IsRevitInstalled && v.IsUpdateAvailable);
        public int AvailableUpdatesCount => RevitVersions.Count(v => v.IsRevitInstalled && v.IsUpdateAvailable);
        public string UpdateAllButtonText => AvailableUpdatesCount > 0 ? $"Обновить все ({AvailableUpdatesCount})" : "Обновить все";

        // Commands
        public ICommand NavigateCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand CheckForUpdatesCommand => CheckUpdatesCommand; // Alias for binding
        public ICommand InstallCommand { get; }
        public ICommand ReinstallCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand CloseWarningCommand { get; }
        public ICommand CheckRevitClosedCommand { get; }
        public ICommand GracefulCloseRevitCommand { get; }
        public ICommand ForceKillRevitCommand { get; }

        public MainViewModel()
        {
            _detectorService = new RevitDetectorService();
            _updateService = new UpdateService();

            // Initialize Commands
            NavigateCommand = new RelayCommand<string>(tab => CurrentTab = tab ?? "plugins");
            CheckUpdatesCommand = new RelayCommand(async () => await CheckUpdatesAsync());
            InstallCommand = new RelayCommand<RevitVersionInfo>(async ver => await StartInstallAsync(ver));
            ReinstallCommand = new RelayCommand<RevitVersionInfo>(async ver => await StartInstallAsync(ver));
            UpdateAllCommand = new RelayCommand(async () => await StartUpdateAllAsync());
            UninstallCommand = new RelayCommand<RevitVersionInfo>(ver => Uninstall(ver));
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            
            CloseWarningCommand = new RelayCommand(CancelWarning);
            CheckRevitClosedCommand = new RelayCommand(async () => await CheckRevitClosedAndProceedAsync());
            GracefulCloseRevitCommand = new RelayCommand(async () => await GracefulCloseRevitAndProceedAsync());
            ForceKillRevitCommand = new RelayCommand(async () => await ForceKillRevitAndProceedAsync());

            LoadSettings();
            
            // Initial scan & auto check
            if (OperatingSystem.IsWindows())
            {
                RefreshLocalVersions();
                _ = CheckUpdatesAsync();
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
                await Task.Delay(300);
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
                var cleanLatest = CleanVersion(LatestVersion);
                var latestVer = new Version(cleanLatest);

                foreach (var ver in RevitVersions)
                {
                    ver.AvailableVersion = LatestVersion;

                    if (!ver.IsRevitInstalled)
                    {
                        ver.IsUpdateAvailable = false;
                        ver.StatusDescription = "Revit не установлен";
                        continue;
                    }

                    if (ver.IsPluginInstalled && !string.IsNullOrEmpty(ver.InstalledVersion) && ver.InstalledVersion != "—")
                    {
                        var cleanInstalled = CleanVersion(ver.InstalledVersion);
                        if (Version.TryParse(cleanInstalled, out var installedVer))
                        {
                            if (latestVer > installedVer)
                            {
                                ver.IsUpdateAvailable = true;
                                ver.StatusDescription = $"Доступно обновление до v{LatestVersion}";
                            }
                            else
                            {
                                ver.IsUpdateAvailable = false;
                                ver.StatusDescription = $"Установлена актуальная версия (v{ver.InstalledVersion})";
                            }
                        }
                        else
                        {
                            ver.IsUpdateAvailable = true;
                            ver.StatusDescription = $"Доступно обновление до v{LatestVersion}";
                        }
                    }
                    else
                    {
                        ver.IsUpdateAvailable = false;
                        ver.StatusDescription = "Плагин не установлен";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error evaluating updates: {ex.Message}");
            }

            OnPropertyChanged(nameof(HasUpdatesAvailable));
            OnPropertyChanged(nameof(AvailableUpdatesCount));
            OnPropertyChanged(nameof(UpdateAllButtonText));
        }

        private static string CleanVersion(string v)
        {
            string clean = v.Trim().TrimStart('v', 'V');
            int plusIdx = clean.IndexOf('+');
            if (plusIdx >= 0) clean = clean.Substring(0, plusIdx);
            int spaceIdx = clean.IndexOf(' ');
            if (spaceIdx >= 0) clean = clean.Substring(0, spaceIdx);
            // If it's e.g. "2.4" convert to "2.4.0"
            var parts = clean.Split('.');
            if (parts.Length == 1) clean += ".0.0";
            else if (parts.Length == 2) clean += ".0";
            return clean;
        }

        private async Task StartInstallAsync(RevitVersionInfo? version)
        {
            if (version == null) return;

            // Check if Revit is currently running
            if (_updateService.IsRevitRunning())
            {
                _pendingInstallVersion = version;
                _pendingInstallAll = false;
                RevitWarningStatus = string.Empty;
                ShowRevitWarning = true;
                return;
            }

            await ProceedInstallAsync(version);
        }

        private async Task StartUpdateAllAsync()
        {
            var outdated = RevitVersions.Where(v => v.IsRevitInstalled && v.IsUpdateAvailable).ToList();
            if (!outdated.Any())
            {
                MessageBox.Show("Все установленные версии плагина актуальны!", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check if Revit is running
            if (_updateService.IsRevitRunning())
            {
                _pendingInstallAll = true;
                _pendingInstallVersion = null;
                RevitWarningStatus = string.Empty;
                ShowRevitWarning = true;
                return;
            }

            await ProceedInstallAllAsync();
        }

        private async Task CheckRevitClosedAndProceedAsync()
        {
            if (_updateService.IsRevitRunning())
            {
                RevitWarningStatus = "Autodesk Revit все еще запущен! Закройте его или нажмите «Закрыть Revit корректно».";
                return;
            }

            ShowRevitWarning = false;
            RevitWarningStatus = string.Empty;

            if (_pendingInstallAll)
            {
                _pendingInstallAll = false;
                await ProceedInstallAllAsync();
            }
            else if (_pendingInstallVersion != null)
            {
                var ver = _pendingInstallVersion;
                _pendingInstallVersion = null;
                await ProceedInstallAsync(ver);
            }
        }

        private async Task GracefulCloseRevitAndProceedAsync()
        {
            RevitWarningStatus = "Отправлен запрос на закрытие окон Revit. Сохраните или синхронизируйте файл в диалоге Revit...";
            bool closed = await _updateService.CloseRevitGracefullyAsync(8);
            if (closed)
            {
                ShowRevitWarning = false;
                RevitWarningStatus = string.Empty;

                if (_pendingInstallAll)
                {
                    _pendingInstallAll = false;
                    await ProceedInstallAllAsync();
                }
                else if (_pendingInstallVersion != null)
                {
                    var ver = _pendingInstallVersion;
                    _pendingInstallVersion = null;
                    await ProceedInstallAsync(ver);
                }
            }
            else
            {
                RevitWarningStatus = "Revit ожидает ответа пользователя (сохранение/синхронизация). Завершите диалог в Revit.";
            }
        }

        private async Task ForceKillRevitAndProceedAsync()
        {
            var confirm = MessageBox.Show(
                "ВНИМАНИЕ! Принудительное завершение Revit приведет к потере всех несохраненных данных и сбою синхронизации с хранилищем!\n\nВы точно хотите принудительно завершить процесс Revit?",
                "Подтверждение принудительного закрытия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            _updateService.ForceKillRevit();
            ShowRevitWarning = false;
            RevitWarningStatus = string.Empty;

            if (_pendingInstallAll)
            {
                _pendingInstallAll = false;
                await ProceedInstallAllAsync();
            }
            else if (_pendingInstallVersion != null)
            {
                var ver = _pendingInstallVersion;
                _pendingInstallVersion = null;
                await ProceedInstallAsync(ver);
            }
        }

        private void CancelWarning()
        {
            ShowRevitWarning = false;
            RevitWarningStatus = string.Empty;
            _pendingInstallVersion = null;
            _pendingInstallAll = false;
        }

        private async Task ProceedInstallAsync(RevitVersionInfo version)
        {
            IsLoading = true;
            ProgressText = $"Установка BimboClub для Revit {version.Year}...";
            GlobalProgress = 0;

            var progress = new Progress<double>(val =>
            {
                GlobalProgress = val;
                ProgressText = $"Установка BimboClub ({val:F0}%)...";
            });

            try
            {
                var manifest = await _updateService.FetchManifestAsync(UpdateSource);
                if (manifest == null)
                {
                    throw new Exception("Не удалось загрузить манифест обновления.");
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await _updateService.InstallUpdateAsync(version, manifest, UpdateSource, progress, cts.Token);

                MessageBox.Show($"Плагин BimboClub (v{manifest.LatestVersion}) успешно установлен для Revit {version.Year}!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                if (OperatingSystem.IsWindows())
                {
                    RefreshLocalVersions();
                }
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

        private async Task ProceedInstallAllAsync()
        {
            var outdated = RevitVersions.Where(v => v.IsRevitInstalled && v.IsUpdateAvailable).ToList();
            if (!outdated.Any()) return;

            IsLoading = true;
            GlobalProgress = 0;

            try
            {
                var manifest = await _updateService.FetchManifestAsync(UpdateSource);
                if (manifest == null)
                {
                    throw new Exception("Не удалось загрузить манифест обновления.");
                }

                int total = outdated.Count;
                for (int i = 0; i < total; i++)
                {
                    var ver = outdated[i];
                    ProgressText = $"[{i + 1}/{total}] Обновление BimboClub для Revit {ver.Year}...";

                    var progress = new Progress<double>(val =>
                    {
                        double overall = ((double)i / total * 100.0) + (val / total);
                        GlobalProgress = overall;
                    });

                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    await _updateService.InstallUpdateAsync(ver, manifest, UpdateSource, progress, cts.Token);
                }

                MessageBox.Show($"Все плагины BimboClub успешно обновлены до версии {manifest.LatestVersion}!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                if (OperatingSystem.IsWindows())
                {
                    RefreshLocalVersions();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при массовом обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                GlobalProgress = 0;
            }
        }

        [SupportedOSPlatform("windows")]
        private void Uninstall(RevitVersionInfo? version)
        {
            if (version == null) return;

            var result = MessageBox.Show(
                $"Вы действительно хотите удалить плагин BimboClub для Revit {version.Year}?", 
                "Подтверждение удаления", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                _updateService.UninstallPlugin(version);
                MessageBox.Show($"Плагин BimboClub успешно удален для Revit {version.Year}.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshLocalVersions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Settings load & save
        private class UserSettings
        {
            public string UpdateSource { get; set; } = string.Empty;
            public bool AutoCloseRevit { get; set; } = false;
        }

        private void LoadSettings()
        {
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimboClubManager");
            string configPath = Path.Combine(configDir, "config.json");

            string defaultRawPath = "https://raw.githubusercontent.com/Nesterro/BimboClub/main/updates/update_manifest.json";

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    if (settings != null)
                    {
                        UpdateSource = string.IsNullOrEmpty(settings.UpdateSource) ? defaultRawPath : settings.UpdateSource;
                        AutoCloseRevit = settings.AutoCloseRevit;
                        return;
                    }
                }
                catch
                {
                    // Fallback to defaults
                }
            }

            UpdateSource = defaultRawPath;
            AutoCloseRevit = false;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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
