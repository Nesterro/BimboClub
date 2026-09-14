using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RevitServerManager.Models;

namespace RevitServerManager.Services
{
    public class DownloadItemResult
    {
        public ModelFileViewModel Model { get; set; }
        public bool Success { get; set; }
        public string DestinationFilePath { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public TimeSpan Duration { get; set; }

        public DownloadItemResult(ModelFileViewModel model)
        {
            Model = model;
        }
    }

    public class DownloadBatchResult
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<DownloadItemResult> Items { get; set; } = new();
    }

    public class RevitServerDownloader
    {
        /// <summary>
        /// Автопоиск установленного RevitServerTool.exe для указанной версии или среди всех версий Revit
        /// </summary>
        public static string? FindRevitServerToolPath(string? preferredVersion = null)
        {
            List<string> candidatePaths = new();

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            string[] searchVersions = string.IsNullOrWhiteSpace(preferredVersion)
                ? new[] { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "2019" }
                : new[] { preferredVersion, "2026", "2025", "2024", "2023", "2022", "2021", "2020", "2019" }.Distinct().ToArray();

            foreach (string ver in searchVersions)
            {
                // Revit Tool Command path (e.g. C:\Program Files\Autodesk\Revit 2024\RevitServerToolCommand\RevitServerTool.exe)
                candidatePaths.Add(Path.Combine(programFiles, "Autodesk", $"Revit {ver}", "RevitServerToolCommand", "RevitServerTool.exe"));
                
                // Revit Server Tool path (e.g. C:\Program Files\Autodesk\Revit Server 2024\Tools\RevitServerToolCommand\RevitServerTool.exe)
                candidatePaths.Add(Path.Combine(programFiles, "Autodesk", $"Revit Server {ver}", "Tools", "RevitServerToolCommand", "RevitServerTool.exe"));

                // Root Revit directory fallback
                candidatePaths.Add(Path.Combine(programFiles, "Autodesk", $"Revit {ver}", "RevitServerTool.exe"));
            }

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Скачивание модели с Revit Server через RevitServerTool.exe
        /// </summary>
        public static async Task<DownloadItemResult> DownloadModelAsync(
            string revitServerToolExe,
            string serverName,
            ModelFileViewModel model,
            string destinationFolder,
            bool overwrite,
            bool preserveSubfolders,
            Action<string>? logCallback = null,
            CancellationToken cancellationToken = default)
        {
            var result = new DownloadItemResult(model);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 1. Формирование пути назначения
                string targetDir = destinationFolder;
                if (preserveSubfolders && !string.IsNullOrWhiteSpace(model.FolderPath))
                {
                    string subPath = model.FolderPath.Replace('|', Path.DirectorySeparatorChar);
                    targetDir = Path.Combine(destinationFolder, subPath);
                }

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string destinationFile = Path.Combine(targetDir, model.Name);
                result.DestinationFilePath = destinationFile;

                if (File.Exists(destinationFile) && !overwrite)
                {
                    result.Success = false;
                    result.ErrorMessage = "Файл уже существует, перезапись отключена.";
                    model.Status = "Пропущен (уже существует)";
                    return result;
                }

                // 2. Формирование аргументов для RevitServerTool.exe
                // Формат: createLocalRVT "<modelPath>" -s "<server>" -d "<destinationDir>" [-o]
                // modelPath: e.g. "FolderA\Model.rvt" (слеши обратные \)
                string relativePath = model.ServerRelativeModelPath.Replace('/', '\\').Replace('|', '\\');
                string overwriteFlag = overwrite ? "-o" : "";
                string arguments = $"createLocalRVT \"{relativePath}\" -s \"{serverName}\" -d \"{targetDir}\" {overwriteFlag}".Trim();

                logCallback?.Invoke($"Запуск: {Path.GetFileName(revitServerToolExe)} {arguments}");
                model.Status = "Скачивание...";

                var startInfo = new ProcessStartInfo
                {
                    FileName = revitServerToolExe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        logCallback?.Invoke($"[{model.Name}] {e.Data}");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        logCallback?.Invoke($"[{model.Name}] ОШИБКА: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0 && File.Exists(destinationFile))
                {
                    result.Success = true;
                    model.Status = "Успешно скачан";
                    model.Progress = 100;
                    logCallback?.Invoke($"Успешно скачан: {model.Name} ({model.FormattedSize})");
                }
                else
                {
                    result.Success = false;
                    string err = errorBuilder.Length > 0 ? errorBuilder.ToString() : outputBuilder.ToString();
                    result.ErrorMessage = string.IsNullOrWhiteSpace(err) ? $"Код завершения: {process.ExitCode}" : err.Trim();
                    model.Status = "Ошибка";
                    logCallback?.Invoke($"Ошибка скачивания {model.Name}: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Отменено пользователем";
                model.Status = "Отменено";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                model.Status = $"Ошибка: {ex.Message}";
                logCallback?.Invoke($"Исключение при скачивании {model.Name}: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }
    }
}
