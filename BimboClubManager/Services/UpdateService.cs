using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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
        public string? LastFetchError { get; private set; }

        public UpdateService()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = true,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BimboClubManager/2.4 (Windows; Revit Plugin Update Manager)");
        }

        public class UpdateManifest
        {
            [JsonIgnore]
            public string? SourceDirectory { get; set; }
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
            LastFetchError = null;
            string clean = (sourcePath ?? string.Empty).Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(clean))
            {
                clean = "https://raw.githubusercontent.com/Nesterro/BimboClub/main/updates/update_manifest.json";
            }

            // 1. Try as local file or network share path
            if (IsLocalOrUncPath(clean))
            {
                var localManifest = await TryFetchLocalManifestAsync(clean);
                if (localManifest != null) return localManifest;
            }

            // 2. Try as HTTP / HTTPS / GitHub URL
            var httpManifest = await TryFetchHttpManifestAsync(clean);
            if (httpManifest != null) return httpManifest;

            // 3. Fallback to default raw GitHub repository if clean was custom and failed
            if (!clean.Equals("https://raw.githubusercontent.com/Nesterro/BimboClub/main/updates/update_manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                var fallbackManifest = await TryFetchHttpManifestAsync("https://raw.githubusercontent.com/Nesterro/BimboClub/main/updates/update_manifest.json");
                if (fallbackManifest != null) return fallbackManifest;
            }

            return null;
        }

        private static bool IsLocalOrUncPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (path.StartsWith(@"\\") || path.Contains(@":\") || path.Contains(@":/"))
            {
                return true;
            }
            if (Directory.Exists(path) || File.Exists(path))
            {
                return true;
            }
            return !path.Contains("github.com") && !path.Contains("/");
        }

        private async Task<UpdateManifest?> TryFetchLocalManifestAsync(string rawPath)
        {
            var candidates = new List<string>();

            if (File.Exists(rawPath))
            {
                candidates.Add(rawPath);
            }

            candidates.Add(Path.Combine(rawPath, "update_manifest.json"));
            candidates.Add(Path.Combine(rawPath, "updates", "update_manifest.json"));

            string? dir = Directory.Exists(rawPath) ? rawPath : Path.GetDirectoryName(rawPath);
            if (!string.IsNullOrEmpty(dir))
            {
                candidates.Add(Path.Combine(dir, "update_manifest.json"));
                candidates.Add(Path.Combine(dir, "updates", "update_manifest.json"));
            }

            foreach (var file in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(file))
                    {
                        string json = await File.ReadAllTextAsync(file);
                        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
                        if (manifest != null && !string.IsNullOrEmpty(manifest.LatestVersion))
                        {
                            manifest.SourceDirectory = Path.GetDirectoryName(Path.GetFullPath(file));
                            LastFetchError = null;
                            return manifest;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LastFetchError = $"Ошибка чтения локального файла '{file}': {ex.Message}";
                    Debug.WriteLine(LastFetchError);
                }
            }

            if (string.IsNullOrEmpty(LastFetchError))
            {
                LastFetchError = $"Файл update_manifest.json не найден по локальному пути: {rawPath}";
            }
            return null;
        }

        private async Task<UpdateManifest?> TryFetchHttpManifestAsync(string input)
        {
            string owner = "Nesterro";
            string repo = "BimboClub";
            string branch = "main";
            string relPath = "updates/update_manifest.json";

            string normalized = input;
            if (normalized.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "https://" + normalized;
            }
            else if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                     !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                     normalized.Contains('/') && !normalized.Contains('\\'))
            {
                normalized = "https://github.com/" + normalized;
            }

            var candidateUrls = new List<string>();

            if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
            {
                string host = uri.Host.ToLowerInvariant();
                string[] segs = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (host == "github.com" || host == "raw.githubusercontent.com")
                {
                    if (segs.Length >= 2)
                    {
                        owner = segs[0];
                        repo = segs[1];
                    }

                    if (host == "raw.githubusercontent.com")
                    {
                        if (segs.Length >= 3) branch = segs[2];
                        if (segs.Length >= 4) relPath = string.Join('/', segs.Skip(3));
                    }
                    else if (host == "github.com")
                    {
                        if (segs.Length >= 4 && (segs[2] == "blob" || segs[2] == "raw" || segs[2] == "tree"))
                        {
                            branch = segs[3];
                            if (segs.Length >= 5)
                            {
                                relPath = string.Join('/', segs.Skip(4));
                                if (!relPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                {
                                    relPath = relPath.TrimEnd('/') + "/update_manifest.json";
                                }
                            }
                        }
                    }

                    candidateUrls.Add($"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{relPath}");
                    candidateUrls.Add($"https://github.com/{owner}/{repo}/raw/{branch}/{relPath}");
                    candidateUrls.Add($"https://cdn.jsdelivr.net/gh/{owner}/{repo}@{branch}/{relPath}");
                    candidateUrls.Add($"https://api.github.com/repos/{owner}/{repo}/contents/{relPath}?ref={branch}");
                }
                else
                {
                    candidateUrls.Add(normalized);
                }
            }
            else
            {
                candidateUrls.Add(normalized);
            }

            candidateUrls.Add("https://raw.githubusercontent.com/Nesterro/BimboClub/main/updates/update_manifest.json");
            candidateUrls.Add("https://github.com/Nesterro/BimboClub/raw/main/updates/update_manifest.json");
            candidateUrls.Add("https://cdn.jsdelivr.net/gh/Nesterro/BimboClub@main/updates/update_manifest.json");
            candidateUrls.Add("https://api.github.com/repos/Nesterro/BimboClub/contents/updates/update_manifest.json");

            var distinctUrls = candidateUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string? lastErrMsg = null;

            foreach (string rawUrl in distinctUrls)
            {
                try
                {
                    string fetchUrl = rawUrl;
                    if (!fetchUrl.Contains("api.github.com"))
                    {
                        fetchUrl += (fetchUrl.Contains("?") ? "&" : "?") + $"_nocache={DateTime.UtcNow.Ticks}";
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                    {
                        NoCache = true,
                        NoStore = true,
                        MustRevalidate = true
                    };

                    if (fetchUrl.Contains("api.github.com"))
                    {
                        request.Headers.Accept.Clear();
                        request.Headers.Accept.ParseAdd("application/vnd.github.v3.raw");
                    }

                    using var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        lastErrMsg = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) от {rawUrl}";
                        continue;
                    }

                    string json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    string trimmed = json.Trim();
                    if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                    {
                        lastErrMsg = $"Ответ содержит HTML страницу вместо JSON манифеста: {rawUrl}";
                        continue;
                    }

                    var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
                    if (manifest != null && !string.IsNullOrEmpty(manifest.LatestVersion))
                    {
                        LastFetchError = null;
                        return manifest;
                    }
                }
                catch (Exception ex)
                {
                    lastErrMsg = $"Ошибка загрузки из {rawUrl}: {ex.Message}";
                    Debug.WriteLine(lastErrMsg);
                }
            }

            LastFetchError = lastErrMsg ?? "Не удалось получить манифест обновлений ни с одного из сетевых источников.";
            return null;
        }

        public bool IsRevitRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("Revit");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public Process[] GetRevitProcesses()
        {
            try
            {
                return Process.GetProcessesByName("Revit");
            }
            catch
            {
                return Array.Empty<Process>();
            }
        }

        public async Task<bool> CloseRevitGracefullyAsync(int timeoutSeconds = 10)
        {
            var processes = GetRevitProcesses();
            if (processes.Length == 0) return true;

            foreach (var proc in processes)
            {
                try
                {
                    proc.CloseMainWindow();
                }
                catch { }
            }

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                if (!IsRevitRunning()) return true;
                await Task.Delay(500);
            }

            return !IsRevitRunning();
        }

        public void ForceKillRevit()
        {
            var processes = GetRevitProcesses();
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to terminate Revit process: {ex.Message}");
                }
            }
        }

        public void TerminateRevit()
        {
            ForceKillRevit();
        }

        public async Task InstallUpdateAsync(
            RevitVersionInfo versionInfo, 
            UpdateManifest manifest, 
            string sourcePath, 
            IProgress<double> progress, 
            CancellationToken cancellationToken)
        {
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
                string? localZip = FindLocalPackage(manifest.SourceDirectory ?? sourcePath, packageUrlOrPath);
                if (!string.IsNullOrEmpty(localZip) && File.Exists(localZip))
                {
                    progress.Report(40);
                    File.Copy(localZip, tempZipPath, true);
                    progress.Report(50);
                }
                else if (packageUrlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                         packageUrlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    await DownloadFileWithFallbacksAsync(packageUrlOrPath, tempZipPath, progress, cancellationToken);
                }
                else
                {
                    string localDir = Directory.Exists(sourcePath) ? sourcePath : (Path.GetDirectoryName(sourcePath) ?? sourcePath);
                    string localZipPath = Path.IsPathRooted(packageUrlOrPath)
                        ? packageUrlOrPath
                        : Path.Combine(localDir, packageUrlOrPath);

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
                cancellationToken.ThrowIfCancellationRequested();
                ValidatePackage(tempExtractPath, manifest.LatestVersion);

                progress.Report(70);

                // 4. Copy to Target Directory
                string targetDir = Path.GetDirectoryName(versionInfo.AddinPath) ?? 
                    throw new Exception("Невозможно определить путь установки.");

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Copy files with retry to handle transient file locks
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

                    CopyFileWithRetry(file, destFile);

                    double copyProgress = 70.0 + (20.0 * (i + 1) / fileCount);
                    progress.Report(copyProgress);
                }

                // 5. Write version.txt
                // 6. Verify / Write Addin Manifest
                progress.Report(95);
                WriteAddinManifest(versionInfo.AddinPath);
                File.WriteAllText(Path.Combine(targetDir, "version.txt"), manifest.LatestVersion);

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

        private static void CopyFileWithRetry(string source, string dest, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    File.Copy(source, dest, true);
                    return;
                }
                catch (IOException) when (attempt < maxRetries)
                {
                    Thread.Sleep(500);
                }
            }
        }

        internal static string? FindLocalPackage(string sourcePath, string packageUrlOrPath)
        {
            // A URL identifies an exact release. Never substitute a same-named local archive.
            if (Uri.TryCreate(packageUrlOrPath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                return null;
            if (Path.IsPathRooted(packageUrlOrPath))
                return Path.GetFullPath(packageUrlOrPath);
            string cleanSource = sourcePath.Trim().Trim('"', '\'');
            if (!IsLocalOrUncPath(cleanSource))
                return null;
            string directory = Directory.Exists(cleanSource) ? cleanSource :
                Path.GetDirectoryName(Path.GetFullPath(cleanSource))!;
            return Path.GetFullPath(Path.Combine(directory, packageUrlOrPath));
        }

        internal static void ValidatePackage(string directory, string expectedVersion)
        {
            string dll = Path.Combine(directory, "BimboClub.dll");
            if (!File.Exists(dll))
                throw new InvalidDataException("Пакет не содержит BimboClub.dll.");
            string actual = FileVersionInfo.GetVersionInfo(dll).FileVersion ?? "";
            if (!Version.TryParse(actual, out var actualVersion) ||
                !Version.TryParse(expectedVersion.TrimStart('v', 'V'), out var expected) ||
                new Version(actualVersion.Major, actualVersion.Minor, Math.Max(0, actualVersion.Build), Math.Max(0, actualVersion.Revision)) !=
                new Version(expected.Major, expected.Minor, Math.Max(0, expected.Build), Math.Max(0, expected.Revision)))
                throw new InvalidDataException($"Версия DLL ({actual}) не совпадает с манифестом ({expectedVersion}).");
        }

        private async Task DownloadFileWithFallbacksAsync(string primaryUrl, string destinationPath, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var candidateUrls = new List<string> { primaryUrl };

            if (primaryUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase) && primaryUrl.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase))
            {
                candidateUrls.Add("https://ghproxy.net/" + primaryUrl);
                candidateUrls.Add(primaryUrl.Replace("github.com", "download.fastgit.org"));
            }

            Exception? lastException = null;
            foreach (var url in candidateUrls.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    await DownloadFileAsync(url, destinationPath, progress, cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.WriteLine($"Failed to download from '{url}': {ex.Message}");
                    if (File.Exists(destinationPath))
                    {
                        try { File.Delete(destinationPath); } catch { }
                    }
                }
            }

            throw new Exception($"Не удалось скачать пакет обновления по адресу: {primaryUrl}. Ошибка: {lastException?.Message}", lastException);
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
            DeleteFileIfExists(Path.Combine(targetDir, "version.txt"));

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
