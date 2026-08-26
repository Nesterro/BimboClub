using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BimboClubManager.Models;

namespace BimboClubManager.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BimboClubManager/2.0 (Windows; Revit Plugin Update Manager)");
        }

        public class UpdateManifest
        {
            [JsonPropertyName("latestVersion")]
            public string LatestVersion { get; set; } = string.Empty;

            [JsonPropertyName("releaseDate")]
            public string ReleaseDate { get; set; } = string.Empty;

            [JsonPropertyName("changelog")]
            public string[] Changelog { get; set; } = Array.Empty<string>();

            [JsonPropertyName("packages")]
            public PackageInfo Packages { get; set; } = new();
        }

        public class PackageInfo
        {
            [JsonPropertyName("net48")]
            public string Net48Url { get; set; } = string.Empty;

            [JsonPropertyName("net8")]
            public string Net8Url { get; set; } = string.Empty;
        }

        public async Task<UpdateManifest?> FetchManifestAsync(string sourcePath)
        {
            try
            {
                string jsonContent;
                if (sourcePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    sourcePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    string url = sourcePath;
                    url += (url.Contains("?") ? "&" : "?") + $"_nocache={DateTime.UtcNow.Ticks}";
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                    {
                        NoCache = true,
                        NoStore = true,
                        MustRevalidate = true
                    };
                    using var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    jsonContent = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    // Treat as local file path or network share
                    string manifestPath = Path.Combine(sourcePath, "update_manifest.json");
                    if (!File.Exists(manifestPath))
                    {
                        return null;
                    }
                    jsonContent = await File.ReadAllTextAsync(manifestPath);
                }

                return JsonSerializer.Deserialize<UpdateManifest>(jsonContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching update manifest: {ex.Message}");
                return null;
            }
        }

        public bool IsRevitRunning()
        {
            var processes = Process.GetProcessesByName("Revit");
            return processes.Length > 0;
        }

        public void TerminateRevit()
        {
            var processes = Process.GetProcessesByName("Revit");
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to terminate Revit process: {ex.Message}");
                }
            }
        }

        public async Task InstallUpdateAsync(
            RevitVersionInfo versionInfo, 
            UpdateManifest manifest, 
            string sourcePath, 
            IProgress<double> progress, 
            CancellationToken cancellationToken)
        {
            // 1. Determine package source (URL or local path)
            string packageUrlOrPath = versionInfo.TargetFramework == "net48" 
                ? manifest.Packages.Net48Url 
                : manifest.Packages.Net8Url;

            if (string.IsNullOrEmpty(packageUrlOrPath))
            {
                throw new Exception($"Не указан путь к пакету обновления для {versionInfo.TargetFramework}");
            }

            string tempZipPath = Path.Combine(Path.GetTempPath(), $"BimboClub_{versionInfo.Year}_{Guid.NewGuid()}.zip");
            string tempExtractPath = Path.Combine(Path.GetTempPath(), $"BimboClub_Extract_{versionInfo.Year}_{Guid.NewGuid()}");

            try
            {
                progress.Report(10); // Start download

                // 2. Download / Copy package
                if (packageUrlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    packageUrlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    await DownloadFileAsync(packageUrlOrPath, tempZipPath, progress, cancellationToken);
                }
                else
                {
                    // Treat as local path (either absolute or relative to sourcePath)
                    string localZipPath = Path.IsPathRooted(packageUrlOrPath)
                        ? packageUrlOrPath
                        : Path.Combine(sourcePath, packageUrlOrPath);

                    if (!File.Exists(localZipPath))
                    {
                        throw new FileNotFoundException($"Пакет обновления не найден по пути: {localZipPath}");
                    }

                    progress.Report(40);
                    File.Copy(localZipPath, tempZipPath, true);
                    progress.Report(50);
                }

                // 3. Extract package
                progress.Report(60);
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
                Directory.CreateDirectory(tempExtractPath);
                ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

                progress.Report(70);

                // 4. Copy to Target Directory
                string targetDir = Path.GetDirectoryName(versionInfo.AddinPath) ?? 
                    throw new Exception("Невозможно определить путь установки.");

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Copy files
                var files = Directory.GetFiles(tempExtractPath, "*.*", SearchOption.AllDirectories);
                int fileCount = files.Length;
                for (int i = 0; i < fileCount; i++)
                {
                    string file = files[i];
                    string relativePath = Path.GetRelativePath(tempExtractPath, file);
                    string destFile = Path.Combine(targetDir, relativePath);

                    string? destFolder = Path.GetDirectoryName(destFile);
                    if (destFolder != null && !Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }

                    File.Copy(file, destFile, true);

                    // Update progress from 70% to 90%
                    double copyProgress = 70.0 + (20.0 * (i + 1) / fileCount);
                    progress.Report(copyProgress);
                }

                // 5. Verify / Write Addin Manifest
                progress.Report(95);
                WriteAddinManifest(versionInfo.AddinPath);

                progress.Report(100);
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                    if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clean up temp files: {ex.Message}");
                }
            }
        }

        public void UninstallPlugin(RevitVersionInfo versionInfo)
        {
            string targetDir = Path.GetDirectoryName(versionInfo.AddinPath) ?? 
                throw new Exception("Невозможно определить путь установки.");

            if (!Directory.Exists(targetDir)) return;

            // Delete specific BimboClub files
            DeleteFileIfExists(versionInfo.AddinPath);
            DeleteFileIfExists(versionInfo.DllPath);
            DeleteFileIfExists(Path.Combine(targetDir, "DuctSystemParamCopy.dll"));
            DeleteFileIfExists(Path.Combine(targetDir, "icon32.png"));

            // Delete icons
            string[] icons = {
                "icon_3d.png", "icon_tags.png", "icon_wall.png", "icon_floor.png", 
                "icon_copy.png", "icon_sizer.png", "icon_print.png", "icon_specs.png", 
                "icon_router.png", "icon_filters.png", "icon_rename.png", 
                "icon_network.png", "icon_param.png", "icon_json.png"
            };

            foreach (var icon in icons)
            {
                DeleteFileIfExists(Path.Combine(targetDir, icon));
            }

            // If folder is empty, delete it
            try
            {
                if (Directory.GetFiles(targetDir).Length == 0 && Directory.GetDirectories(targetDir).Length == 0)
                {
                    Directory.Delete(targetDir);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete empty folder {targetDir}: {ex.Message}");
            }
        }

        private void DeleteFileIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete file {path}: {ex.Message}");
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalReadBytes = 0L;
            var bytesRead = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalReadBytes += bytesRead;

                if (totalBytes != -1)
                {
                    // Map download progress to 10% - 40% range
                    double downloadProgress = 10.0 + (30.0 * totalReadBytes / totalBytes);
                    progress.Report(downloadProgress);
                }
            }
        }

        private void WriteAddinManifest(string path)
        {
            string xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RevitAddIns>
  <AddIn Type=""Application"">
    <Name>BimboClub Tools</Name>
    <Assembly>BimboClub.dll</Assembly>
    <AddInId>B13B0C1B-C10B-4DDE-A3C9-968B750A8610</AddInId>
    <FullClassName>BimboClub.App</FullClassName>
    <VendorId>BMBC</VendorId>
    <VendorDescription>BimboClub</VendorDescription>
  </AddIn>
</RevitAddIns>";

            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, xmlContent, System.Text.Encoding.UTF8);
        }
    }
}
