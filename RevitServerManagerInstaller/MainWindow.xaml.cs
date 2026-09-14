using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace RevitServerManagerInstaller
{
    public partial class MainWindow : Window
    {
        private const string AppName = "BimboClub Revit Server Manager";
        private const string AppExecutableName = "RevitServerManager.exe";
        private const string AppVersion = "2.3.0";

        public MainWindow()
        {
            InitializeComponent();

            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "BimboClub",
                "RevitServerManager");

            InstallPathTextBox.Text = defaultPath;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку для установки BimboClub Revit Server Manager",
                InitialDirectory = InstallPathTextBox.Text
            };

            if (dialog.ShowDialog(this) == true)
            {
                InstallPathTextBox.Text = dialog.FolderName;
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            string installPath = InstallPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installPath))
            {
                MessageBox.Show(this, "Пожалуйста, укажите путь для установки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            InstallPathTextBox.IsEnabled = false;
            DesktopShortcutCheckBox.IsEnabled = false;
            StartMenuShortcutCheckBox.IsEnabled = false;
            LaunchAfterInstallCheckBox.IsEnabled = false;

            ProgressSection.Visibility = Visibility.Visible;
            InstallProgressBar.IsIndeterminate = true;
            StatusTextBlock.Text = "Копирование исполняемых файлов...";

            bool createDesktopShortcut = DesktopShortcutCheckBox.IsChecked == true;
            bool createStartMenuShortcut = StartMenuShortcutCheckBox.IsChecked == true;
            bool launchAfter = LaunchAfterInstallCheckBox.IsChecked == true;

            try
            {
                await Task.Run(() =>
                {
                    // 1. Create target directory
                    if (!Directory.Exists(installPath))
                    {
                        Directory.CreateDirectory(installPath);
                    }

                    // 2. Extract embedded executable
                    string targetExePath = Path.Combine(installPath, AppExecutableName);
                    
                    // Stop running process if exists
                    foreach (var p in Process.GetProcessesByName("RevitServerManager"))
                    {
                        try { p.Kill(); p.WaitForExit(2000); } catch { }
                    }

                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = "RevitServerManagerInstaller.Payload.RevitServerManager.exe";

                    using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            // Fallback: search any resource ending with RevitServerManager.exe
                            foreach (var res in assembly.GetManifestResourceNames())
                            {
                                if (res.EndsWith("RevitServerManager.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    using var fallbackStream = assembly.GetManifestResourceStream(res)!;
                                    using var fs = new FileStream(targetExePath, FileMode.Create, FileAccess.Write);
                                    fallbackStream.CopyTo(fs);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            using var fs = new FileStream(targetExePath, FileMode.Create, FileAccess.Write);
                            stream.CopyTo(fs);
                        }
                    }

                    // 3. Create Desktop Shortcut
                    if (createDesktopShortcut)
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string shortcutFile = Path.Combine(desktopPath, $"{AppName}.lnk");
                        CreateShellShortcut(shortcutFile, targetExePath);
                    }

                    // 4. Create Start Menu Shortcut
                    if (createStartMenuShortcut)
                    {
                        string startMenuPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                            "Programs",
                            "BimboClub");

                        if (!Directory.Exists(startMenuPath))
                        {
                            Directory.CreateDirectory(startMenuPath);
                        }

                        string shortcutFile = Path.Combine(startMenuPath, $"{AppName}.lnk");
                        CreateShellShortcut(shortcutFile, targetExePath);
                    }

                    // 5. Register in Windows Add/Remove Programs (Registry)
                    RegisterUninstall(installPath, targetExePath);
                });

                StatusTextBlock.Text = "Установка успешно завершена!";
                InstallProgressBar.IsIndeterminate = false;
                InstallProgressBar.Value = 100;

                string targetExe = Path.Combine(installPath, AppExecutableName);

                if (launchAfter && File.Exists(targetExe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = targetExe,
                            UseShellExecute = true,
                            WorkingDirectory = installPath
                        });
                    }
                    catch { }
                }

                MessageBox.Show(this, "BimboClub Revit Server Manager успешно установлен на ваш компьютер!", "Установка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка при установке";
                InstallProgressBar.IsIndeterminate = false;
                MessageBox.Show(this, $"Не удалось завершить установку:\n\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                InstallButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        private static void CreateShellShortcut(string shortcutPath, string targetExePath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetExePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
                    shortcut.Description = AppName;
                    shortcut.IconLocation = targetExePath + ",0";
                    shortcut.Save();
                }
            }
            catch { }
        }

        private static void RegisterUninstall(string installPath, string targetExePath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\BimboClubRevitServerManager");
                if (key != null)
                {
                    key.SetValue("DisplayName", AppName);
                    key.SetValue("DisplayVersion", AppVersion);
                    key.SetValue("Publisher", "BimboClub");
                    key.SetValue("InstallLocation", installPath);
                    key.SetValue("DisplayIcon", targetExePath);
                    key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{installPath}\"");
                }
            }
            catch { }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
