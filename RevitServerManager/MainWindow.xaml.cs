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
        private RevitServerRestClient? _client;
        private string? _detectedToolPath;
        private CancellationTokenSource? _downloadCts;

        public ObservableCollection<FolderViewModel> RootFolders { get; } = new();
        public ObservableCollection<ModelFileViewModel> CurrentFolderModels { get; } = new();
        private readonly Dictionary<string, ModelFileViewModel> _selectedModels = new(StringComparer.OrdinalIgnoreCase);

        private ICollectionView? _modelsView;
        private FolderViewModel? _currentSelectedFolder;

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            ApplySettingsToUi();

            FoldersTreeView.ItemsSource = RootFolders;
            ModelsListView.ItemsSource = CurrentFolderModels;

            _modelsView = CollectionViewSource.GetDefaultView(CurrentFolderModels);
            _modelsView.Filter = FilterModelItem;

            CheckRevitToolAvailability();
        }

        private void ApplySettingsToUi()
        {
            ServerAddressComboBox.Items.Clear();
            foreach (var srv in _settings.RecentServers)
            {
                ServerAddressComboBox.Items.Add(srv);
            }
            ServerAddressComboBox.Text = _settings.ServerAddress;

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
            if (ServerVersionComboBox.SelectedItem is ComboBoxItem item)
            {
                _settings.ServerVersion = item.Content?.ToString() ?? "2024";
            }
            _settings.DestinationFolder = DestinationFolderTextBox.Text.Trim();
            _settings.OverwriteExisting = OverwriteCheckBox.IsChecked == true;
            _settings.PreserveSubfolders = PreserveSubfoldersCheckBox.IsChecked == true;
            _settings.OpenFolderOnComplete = OpenFolderCheckBox.IsChecked == true;
            _settings.Save();
        }

        private void CheckRevitToolAvailability()
        {
            string? preferredVer = (ServerVersionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            
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

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string host = ServerAddressComboBox.Text.Trim();
            string ver = (ServerVersionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "2024";

            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Пожалуйста, введите адрес сервера.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveUiSettings();
            CheckRevitToolAvailability();

            ConnectButton.IsEnabled = false;
            StatusBadgeText.Text = "Подключение...";
            StatusIndicator.Fill = (SolidColorBrush)FindResource("WarningYellowBrush");
            ProgressStatusTextBlock.Text = $"Подключение к http://{host}:{ver}...";

            RootFolders.Clear();
            CurrentFolderModels.Clear();
            _selectedModels.Clear();
            UpdateSummaryText();

            try
            {
                _client?.Dispose();
                _client = new RevitServerRestClient(host, ver);

                var props = await _client.CheckConnectionAsync();

                StatusBadgeText.Text = $"{props.ServerName} ({props.ServerVersion})";
                StatusIndicator.Fill = (SolidColorBrush)FindResource("SuccessGreenBrush");

                ProgressStatusTextBlock.Text = "Загрузка дерева каталогов...";
                var rootContents = await _client.GetContentsAsync("|");

                if (rootContents?.Folders != null)
                {
                    foreach (var folder in rootContents.Folders)
                    {
                        bool hasSubfolders = folder.FolderCount > 0;
                        RootFolders.Add(new FolderViewModel(folder.Name, folder.Name, hasSubfolders));
                    }
                }

                ProgressStatusTextBlock.Text = $"Подключено к {props.ServerName}. Доступно папок: {RootFolders.Count}";
            }
            catch (Exception ex)
            {
                _client?.Dispose();
                _client = null;
                StatusBadgeText.Text = "Ошибка подключения";
                StatusIndicator.Fill = (SolidColorBrush)FindResource("ErrorRedBrush");
                ProgressStatusTextBlock.Text = "Ошибка подключения к Revit Server";
                MessageBox.Show(this, $"Не удалось подключиться к Revit Server ({host}, {ver}):\n\n{ex.Message}", "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private void RefreshFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client != null)
            {
                ConnectButton_Click(sender, e);
            }
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TreeViewItem treeViewItem || treeViewItem.Header is not FolderViewModel folder) return;
            if (folder.IsLoaded || _client == null) return;

            try
            {
                ProgressStatusTextBlock.Text = $"Загрузка подпапок для {folder.Name}...";
                var contents = await _client.GetContentsAsync(folder.ServerRelativePath);

                folder.SubFolders.Clear();
                if (contents?.Folders != null)
                {
                    foreach (var sub in contents.Folders)
                    {
                        bool hasSub = sub.FolderCount > 0;
                        string relPath = $"{folder.ServerRelativePath}|{sub.Name}";
                        folder.SubFolders.Add(new FolderViewModel(sub.Name, relPath, hasSub));
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

        private async void FoldersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FoldersTreeView.SelectedItem is not FolderViewModel selectedFolder || _client == null)
            {
                _currentSelectedFolder = null;
                CurrentPathTextBlock.Text = "Не выбрана";
                CurrentFolderModels.Clear();
                ModelsCountTextBlock.Text = "Моделей: 0";
                return;
            }

            _currentSelectedFolder = selectedFolder;
            CurrentPathTextBlock.Text = selectedFolder.ServerRelativePath.Replace("|", " / ");

            try
            {
                ProgressStatusTextBlock.Text = $"Получение списка моделей из {selectedFolder.Name}...";
                var contents = await _client.GetContentsAsync(selectedFolder.ServerRelativePath);

                CurrentFolderModels.Clear();

                if (contents?.Models != null)
                {
                    foreach (var m in contents.Models)
                    {
                        var modelVm = new ModelFileViewModel(m.Name, selectedFolder.ServerRelativePath, m.Size);
                        string key = modelVm.ServerRelativeModelPath;

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
                MessageBox.Show(this, $"Ошибка загрузки моделей:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool FilterModelItem(object item)
        {
            if (item is not ModelFileViewModel model) return false;

            string query = SearchModelsTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query) || query == "Поиск модели...") return true;

            return model.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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
                string key = modelVm.ServerRelativeModelPath;
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
                    _selectedModels[model.ServerRelativeModelPath] = model;
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
                    _selectedModels.Remove(model.ServerRelativeModelPath);
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
                    string key = model.ServerRelativeModelPath;
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

            string host = _client?.Host ?? ServerAddressComboBox.Text.Trim();
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
                ProgressStatusTextBlock.Text = $"Загрузка ({i + 1}/{total}): {model.Name}...";

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
                        errorDetails.Add($"{model.Name}: {itemResult.ErrorMessage}");
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
            _client?.Dispose();
            base.OnClosed(e);
        }
    }
}
