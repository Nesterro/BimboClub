using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RevitServerManager.Models;
using RevitServerManager.Services;

namespace RevitServerManager
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private string? _detectedToolPath;
        private CancellationTokenSource? _downloadCts;

        // Tree items can be ServerNodeViewModel or FolderViewModel
        public ObservableCollection<object> RootTreeItems { get; } = new();
        public ObservableCollection<ModelFileViewModel> CurrentFolderModels { get; } = new();
        private readonly Dictionary<string, ModelFileViewModel> _selectedModels = new(StringComparer.OrdinalIgnoreCase);

        private ICollectionView? _modelsView;
        private readonly Dictionary<string, RevitServerRestClient> _clientsCache = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            ApplySettingsToUi();

            FoldersTreeView.ItemsSource = RootTreeItems;
            ModelsListView.ItemsSource = CurrentFolderModels;

            _modelsView = CollectionViewSource.GetDefaultView(CurrentFolderModels);
            _modelsView.Filter = FilterModelItem;

            CheckRevitToolAvailability();
            LoadRsnServersForSelectedVersion(autoConnectAll: true);
        }

        private string GetSelectedVersion()
        {
            return (ServerVersionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "2024";
        }

        private void ApplySettingsToUi()
        {
            foreach (ComboBoxItem item in ServerVersionComboBox.Items)
            {
                if (item.Content?.ToString() == _settings.ServerVersion)
                {
                    ServerVersionComboBox.SelectedItem = item;
                    break;
                }
            }

            DestinationFolderTextBox.Text = _settings.DestinationFolder;
            OverwriteCheckBox.IsChecked = _settings.OverwriteExisting;
            PreserveSubfoldersCheckBox.IsChecked = _settings.PreserveSubfolders;
            OpenFolderCheckBox.IsChecked = _settings.OpenFolderOnComplete;
        }

        private void SaveUiSettings()
        {
            _settings.ServerAddress = ServerAddressComboBox.Text.Trim();
            _settings.ServerVersion = GetSelectedVersion();
            _settings.DestinationFolder = DestinationFolderTextBox.Text.Trim();
            _settings.OverwriteExisting = OverwriteCheckBox.IsChecked == true;
            _settings.PreserveSubfolders = PreserveSubfoldersCheckBox.IsChecked == true;
            _settings.OpenFolderOnComplete = OpenFolderCheckBox.IsChecked == true;
            _settings.Save();
        }

        private void CheckRevitToolAvailability()
        {
            string preferredVer = GetSelectedVersion();
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomRevitServerToolPath) && File.Exists(_settings.CustomRevitServerToolPath))
            {
                _detectedToolPath = _settings.CustomRevitServerToolPath;
                RevitToolStatusTextBlock.Text = $"Пользовательский ({Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(_detectedToolPath)))})";
                RevitToolStatusTextBlock.Foreground = (SolidColorBrush)FindResource("SuccessGreenBrush");
            }
            else
            {
                _detectedToolPath = RevitServerDownloader.FindRevitServerToolPath(preferredVer);
                if (_detectedToolPath != null)
                {
                    string verFolder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(_detectedToolPath))) ?? "Revit";
                    RevitToolStatusTextBlock.Text = $"Найдена ({verFolder})";
                    RevitToolStatusTextBlock.Foreground = (SolidColorBrush)FindResource("SuccessGreenBrush");
                }
                else
                {
                    RevitToolStatusTextBlock.Text = "Не найдена (укажите путь ⚙️)";
                    RevitToolStatusTextBlock.Foreground = (SolidColorBrush)FindResource("ErrorRedBrush");
                }
            }
        }

        private void ServerVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            CheckRevitToolAvailability();
            LoadRsnServersForSelectedVersion(autoConnectAll: true);
        }

        private void LoadRsnServersForSelectedVersion(bool autoConnectAll = false)
        {
            string version = GetSelectedVersion();
            List<string> rsnServers = RsnConfigService.ReadServersForVersion(version);

            ServerAddressComboBox.Items.Clear();

            // Add RSN servers first
            foreach (var srv in rsnServers)
            {
                ServerAddressComboBox.Items.Add(srv);
            }

            // Add user history servers if not present
            foreach (var srv in _settings.RecentServers)
            {
                if (!rsnServers.Contains(srv, StringComparer.OrdinalIgnoreCase))
                {
                    ServerAddressComboBox.Items.Add(srv);
                }
            }

            if (ServerAddressComboBox.Items.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(_settings.ServerAddress) && ServerAddressComboBox.Items.Contains(_settings.ServerAddress))
                {
                    ServerAddressComboBox.SelectedItem = _settings.ServerAddress;
                }
                else
                {
                    ServerAddressComboBox.SelectedIndex = 0;
                }
            }

            if (autoConnectAll && rsnServers.Count > 0)
            {
                BuildMultiServerTree(rsnServers, version);
            }
        }

        private void BuildMultiServerTree(List<string> servers, string version)
        {
            RootTreeItems.Clear();
            CurrentFolderModels.Clear();
            _selectedModels.Clear();
            UpdateSummaryText();

            foreach (var srv in servers)
            {
                var serverNode = new ServerNodeViewModel(srv, version);
                RootTreeItems.Add(serverNode);
            }

            StatusBadgeText.Text = $"RSN.ini ({version}): {servers.Count} серверов";
            StatusIndicator.Fill = (SolidColorBrush)FindResource("SuccessGreenBrush");
            ProgressStatusTextBlock.Text = $"Загружен список из {servers.Count} серверов из конфигурации RSN.ini ({version}).";
        }

        private RevitServerRestClient GetClientFor(string host, string version)
        {
            string key = $"{host}_{version}";
            if (!_clientsCache.TryGetValue(key, out var client))
            {
                client = new RevitServerRestClient(host, version);
                _clientsCache[key] = client;
            }
            return client;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string host = ServerAddressComboBox.Text.Trim();
            string ver = GetSelectedVersion();

            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Пожалуйста, выберите или введите адрес сервера.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveUiSettings();

            ConnectButton.IsEnabled = false;
            StatusBadgeText.Text = "Подключение...";
            StatusIndicator.Fill = (SolidColorBrush)FindResource("WarningYellowBrush");
            ProgressStatusTextBlock.Text = $"Подключение к {host} ({ver})...";

            RootTreeItems.Clear();
            CurrentFolderModels.Clear();
            _selectedModels.Clear();
            UpdateSummaryText();

            try
            {
                var client = GetClientFor(host, ver);
                var props = await client.CheckConnectionAsync();

                StatusBadgeText.Text = $"{props.ServerName} ({props.ServerVersion})";
                StatusIndicator.Fill = (SolidColorBrush)FindResource("SuccessGreenBrush");

                ProgressStatusTextBlock.Text = $"Загрузка структуры каталогов {props.ServerName}...";
                var rootContents = await client.GetContentsAsync("|");

                if (rootContents?.Folders != null)
                {
                    foreach (var folder in rootContents.Folders)
                    {
                        bool hasSub = folder.FolderCount > 0;
                        RootTreeItems.Add(new FolderViewModel(folder.Name, folder.Name, host, ver, hasSub));
                    }
                }

                ProgressStatusTextBlock.Text = $"Подключено к {props.ServerName}. Корневых папок: {RootTreeItems.Count}";
            }
            catch (Exception ex)
            {
                StatusBadgeText.Text = "Ошибка подключения";
                StatusIndicator.Fill = (SolidColorBrush)FindResource("ErrorRedBrush");
                ProgressStatusTextBlock.Text = $"Ошибка подключения к {host}";
                MessageBox.Show(this, $"Не удалось подключиться к Revit Server ({host}, {ver}):\n\n{ex.Message}", "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private void ConnectAllRsnButton_Click(object sender, RoutedEventArgs e)
        {
            string version = GetSelectedVersion();
            List<string> rsnServers = RsnConfigService.ReadServersForVersion(version);

            if (rsnServers.Count == 0)
            {
                MessageBox.Show(this, $"В файле конфигурации RSN.ini для Revit {version} не найдено серверов.\n\nВы можете открыть и отредактировать RSN.ini по кнопке «📝 Открыть RSN.ini».", "RSN.ini пуст", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BuildMultiServerTree(rsnServers, version);
        }

        private void OpenRsnFileButton_Click(object sender, RoutedEventArgs e)
        {
            string version = GetSelectedVersion();
            string? rsnFile = RsnConfigService.FindExistingRsnFile(version);

            if (rsnFile != null && File.Exists(rsnFile))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{rsnFile}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Не удалось открыть файл:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Create default RSN.ini file in ProgramData
                string defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Autodesk",
                    $"Revit Server {version}",
                    "Config",
                    "RSN.ini");

                try
                {
                    string dir = Path.GetDirectoryName(defaultPath)!;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    if (!File.Exists(defaultPath))
                    {
                        File.WriteAllText(defaultPath, "# Revit Server Network (RSN.ini)\n# Введите адреса серверов по одному на строку, например:\n# 192.168.1.100\n# revit-server.company.local\n");
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{defaultPath}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Не удалось создать файл RSN.ini:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRsnServersForSelectedVersion(autoConnectAll: true);
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TreeViewItem treeViewItem) return;

            // 1. If expanding a Server node (🖥️)
            if (treeViewItem.Header is ServerNodeViewModel serverNode)
            {
                if (serverNode.IsLoaded) return;

                try
                {
                    serverNode.StatusText = "Подключение...";
                    ProgressStatusTextBlock.Text = $"Загрузка каталогов с {serverNode.ServerAddress}...";

                    var client = GetClientFor(serverNode.ServerAddress, serverNode.Version);
                    var rootContents = await client.GetContentsAsync("|");

                    serverNode.SubFolders.Clear();
                    if (rootContents?.Folders != null)
                    {
                        foreach (var f in rootContents.Folders)
                        {
                            bool hasSub = f.FolderCount > 0;
                            serverNode.SubFolders.Add(new FolderViewModel(f.Name, f.Name, serverNode.ServerAddress, serverNode.Version, hasSub));
                        }
                    }

                    serverNode.IsLoaded = true;
                    serverNode.StatusText = $"{serverNode.SubFolders.Count} папок";
                    ProgressStatusTextBlock.Text = $"Сервер {serverNode.ServerAddress} загружен ({serverNode.SubFolders.Count} папок).";
                }
                catch (Exception ex)
                {
                    serverNode.StatusText = "Ошибка";
                    serverNode.IsOnline = false;
                    ProgressStatusTextBlock.Text = $"Ошибка подключения к {serverNode.ServerAddress}: {ex.Message}";
                }
                return;
            }

            // 2. If expanding a Folder node (📁)
            if (treeViewItem.Header is FolderViewModel folder)
            {
                if (folder.IsLoaded) return;

                try
                {
                    ProgressStatusTextBlock.Text = $"Загрузка подпапок {folder.Name} с {folder.ServerAddress}...";
                    var client = GetClientFor(folder.ServerAddress, folder.ServerVersion);
                    var contents = await client.GetContentsAsync(folder.ServerRelativePath);

                    folder.SubFolders.Clear();
                    if (contents?.Folders != null)
                    {
                        foreach (var sub in contents.Folders)
                        {
                            bool hasSub = sub.FolderCount > 0;
                            string relPath = $"{folder.ServerRelativePath}|{sub.Name}";
                            folder.SubFolders.Add(new FolderViewModel(sub.Name, relPath, folder.ServerAddress, folder.ServerVersion, hasSub));
                        }
                    }

                    folder.IsLoaded = true;
                    ProgressStatusTextBlock.Text = "Каталог обновлен";
                }
                catch (Exception ex)
                {
                    if (folder.SubFolders.Count == 0) folder.SubFolders.Add(null);
                    MessageBox.Show(this, $"Не удалось загрузить подпапки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void FoldersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Case 1: Folder selected
            if (FoldersTreeView.SelectedItem is FolderViewModel selectedFolder)
            {
                CurrentPathTextBlock.Text = $"{selectedFolder.ServerAddress} / {selectedFolder.ServerRelativePath.Replace("|", " / ")}";
                await LoadModelsAsync(selectedFolder.ServerAddress, selectedFolder.ServerVersion, selectedFolder.ServerRelativePath);
                return;
            }

            // Case 2: Server node selected
            if (FoldersTreeView.SelectedItem is ServerNodeViewModel selectedServer)
            {
                CurrentPathTextBlock.Text = $"{selectedServer.ServerAddress} (Корень)";
                await LoadModelsAsync(selectedServer.ServerAddress, selectedServer.Version, "|");
                return;
            }

            CurrentPathTextBlock.Text = "Не выбрана папка";
            CurrentFolderModels.Clear();
            ModelsCountTextBlock.Text = "Моделей: 0";
        }

        private async Task LoadModelsAsync(string serverAddress, string serverVersion, string folderPath)
        {
            try
            {
                ProgressStatusTextBlock.Text = $"Получение списка моделей из {serverAddress}...";
                var client = GetClientFor(serverAddress, serverVersion);
                var contents = await client.GetContentsAsync(folderPath);

                CurrentFolderModels.Clear();

                if (contents?.Models != null)
                {
                    foreach (var m in contents.Models)
                    {
                        var modelVm = new ModelFileViewModel(
                            m.Name,
                            folderPath == "|" ? "" : folderPath,
                            m.Size,
                            serverAddress,
                            serverVersion);

                        string key = $"{serverAddress}_{modelVm.ServerRelativeModelPath}";

                        if (_selectedModels.ContainsKey(key))
                        {
                            modelVm.IsSelected = true;
                        }

                        CurrentFolderModels.Add(modelVm);
                    }
                }

                ModelsCountTextBlock.Text = $"Моделей: {CurrentFolderModels.Count}";
                ProgressStatusTextBlock.Text = $"Готово. Найдено моделей: {CurrentFolderModels.Count}";
                _modelsView?.Refresh();
            }
            catch (Exception ex)
            {
                CurrentFolderModels.Clear();
                ModelsCountTextBlock.Text = "Моделей: 0";
                ProgressStatusTextBlock.Text = $"Ошибка загрузки моделей с {serverAddress}: {ex.Message}";
            }
        }

        private bool FilterModelItem(object item)
        {
            if (item is not ModelFileViewModel model) return false;

            string query = SearchModelsTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query) || query == "Поиск модели...") return true;

            return model.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   model.ServerAddress.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SearchModelsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _modelsView?.Refresh();
        }

        private void SearchModelsTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchModelsTextBox.Text == "Поиск модели...")
            {
                SearchModelsTextBox.Text = "";
                SearchModelsTextBox.Foreground = (SolidColorBrush)FindResource("TextWhiteBrush");
            }
        }

        private void SearchModelsTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchModelsTextBox.Text))
            {
                SearchModelsTextBox.Text = "Поиск модели...";
                SearchModelsTextBox.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            }
        }

        private void ModelCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.DataContext is ModelFileViewModel modelVm)
            {
                string key = $"{modelVm.ServerAddress}_{modelVm.ServerRelativeModelPath}";
                if (modelVm.IsSelected)
                {
                    _selectedModels[key] = modelVm;
                }
                else
                {
                    _selectedModels.Remove(key);
                }
                UpdateSummaryText();
            }
        }

        private void SelectAllModels_Click(object sender, RoutedEventArgs e)
        {
            foreach (var model in CurrentFolderModels)
            {
                if (FilterModelItem(model))
                {
                    model.IsSelected = true;
                    string key = $"{model.ServerAddress}_{model.ServerRelativeModelPath}";
                    _selectedModels[key] = model;
                }
            }
            UpdateSummaryText();
        }

        private void SelectNoneModels_Click(object sender, RoutedEventArgs e)
        {
            foreach (var model in CurrentFolderModels)
            {
                if (FilterModelItem(model))
                {
                    model.IsSelected = false;
                    string key = $"{model.ServerAddress}_{model.ServerRelativeModelPath}";
                    _selectedModels.Remove(key);
                }
            }
            UpdateSummaryText();
        }

        private void InvertModelsSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var model in CurrentFolderModels)
            {
                if (FilterModelItem(model))
                {
                    model.IsSelected = !model.IsSelected;
                    string key = $"{model.ServerAddress}_{model.ServerRelativeModelPath}";
                    if (model.IsSelected) _selectedModels[key] = model;
                    else _selectedModels.Remove(key);
                }
            }
            UpdateSummaryText();
        }

        private void UpdateSummaryText()
        {
            int count = _selectedModels.Count;
            long totalBytes = _selectedModels.Values.Sum(m => m.SizeBytes);
            string sizeStr = FormatSize(totalBytes);

            SelectedSummaryTextBlock.Text = $"Выбрано: {count} моделей ({sizeStr})";
            DownloadButton.IsEnabled = count > 0 && !string.IsNullOrWhiteSpace(DestinationFolderTextBox.Text);
        }

        private void BrowseDestinationFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Выберите папку для сохранения моделей с Revit Server",
                InitialDirectory = Directory.Exists(DestinationFolderTextBox.Text) ? DestinationFolderTextBox.Text : ""
            };

            if (dialog.ShowDialog(this) == true)
            {
                DestinationFolderTextBox.Text = dialog.FolderName;
                SaveUiSettings();
                UpdateSummaryText();
            }
        }

        private void ConfigureToolPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Укажите путь к RevitServerTool.exe",
                Filter = "RevitServerTool (RevitServerTool.exe)|RevitServerTool.exe|Все исполняемые файлы (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                _settings.CustomRevitServerToolPath = dialog.FileName;
                _settings.Save();
                CheckRevitToolAvailability();
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedModels.Count == 0)
            {
                MessageBox.Show(this, "Пожалуйста, выберите хотя бы одну модель для скачивания.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string destFolder = DestinationFolderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(destFolder))
            {
                MessageBox.Show(this, "Пожалуйста, укажите папку для сохранения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_detectedToolPath == null || !File.Exists(_detectedToolPath))
            {
                MessageBox.Show(this, "Утилита RevitServerTool.exe не найдена!\nПожалуйста, нажмите кнопку настройки ⚙️ и укажите путь к RevitServerTool.exe вручную.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveUiSettings();

            bool overwrite = OverwriteCheckBox.IsChecked == true;
            bool preserveSub = PreserveSubfoldersCheckBox.IsChecked == true;
            bool openFolder = OpenFolderCheckBox.IsChecked == true;

            var modelsToDownload = _selectedModels.Values.ToList();
            int total = modelsToDownload.Count;

            _downloadCts = new CancellationTokenSource();
            SetUiBusy(true);

            DownloadProgressBar.Minimum = 0;
            DownloadProgressBar.Maximum = total;
            DownloadProgressBar.Value = 0;

            int successCount = 0;
            int failCount = 0;
            List<string> errorDetails = new();

            for (int i = 0; i < total; i++)
            {
                if (_downloadCts.Token.IsCancellationRequested) break;

                var model = modelsToDownload[i];
                string host = !string.IsNullOrWhiteSpace(model.ServerAddress) ? model.ServerAddress : ServerAddressComboBox.Text.Trim();
                ProgressStatusTextBlock.Text = $"Загрузка ({i + 1}/{total}): [{host}] {model.Name}...";

                var itemResult = await Task.Run(() => RevitServerDownloader.DownloadModelAsync(
                    _detectedToolPath,
                    host,
                    model,
                    destFolder,
                    overwrite,
                    preserveSub,
                    null,
                    _downloadCts.Token
                ));

                if (itemResult.Success)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    if (!string.IsNullOrEmpty(itemResult.ErrorMessage))
                    {
                        errorDetails.Add($"[{host}] {model.Name}: {itemResult.ErrorMessage}");
                    }
                }

                DownloadProgressBar.Value = i + 1;
            }

            SetUiBusy(false);

            if (_downloadCts.Token.IsCancellationRequested)
            {
                ProgressStatusTextBlock.Text = "Загрузка прервана пользователем.";
                MessageBox.Show(this, "Скачивание было отменено пользователем.", "Отмена", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProgressStatusTextBlock.Text = $"Завершено. Успешно: {successCount}, Ошибок: {failCount}";

            string summaryMsg = $"Загрузка завершена!\n\n • Успешно скачано моделей: {successCount}\n • Ошибок / Пропущено: {failCount}";
            if (errorDetails.Count > 0)
            {
                summaryMsg += "\n\nДетали ошибок:\n" + string.Join("\n", errorDetails.Take(10));
                if (errorDetails.Count > 10) summaryMsg += $"\n... и еще {errorDetails.Count - 10} ошибок.";
            }

            MessageBox.Show(this, summaryMsg, "Итоги скачивания", MessageBoxButton.OK, failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            if (openFolder && Directory.Exists(destFolder))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = destFolder,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
        }

        private void SetUiBusy(bool isBusy)
        {
            ConnectButton.IsEnabled = !isBusy;
            ConnectAllRsnButton.IsEnabled = !isBusy;
            ServerAddressComboBox.IsEnabled = !isBusy;
            ServerVersionComboBox.IsEnabled = !isBusy;
            FoldersTreeView.IsEnabled = !isBusy;
            ModelsListView.IsEnabled = !isBusy;
            DestinationFolderTextBox.IsEnabled = !isBusy;
            DownloadButton.IsEnabled = !isBusy;
            CancelDownloadButton.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 Б";
            string[] units = { "Б", "КБ", "МБ", "ГБ" };
            double size = bytes;
            int unitIdx = 0;
            while (size >= 1024 && unitIdx < units.Length - 1)
            {
                size /= 1024;
                unitIdx++;
            }
            return $"{size:F1} {units[unitIdx]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveUiSettings();
            foreach (var client in _clientsCache.Values)
            {
                client.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
