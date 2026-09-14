using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RevitServerManager.Models;

namespace RevitServerManager.Services
{
    public static class RevitServerBridgeClient
    {
        private static string? _cachedBridgePath;
        private static readonly object _lock = new();

        public static string? EnsureBridgeExecutable()
        {
            lock (_lock)
            {
                if (_cachedBridgePath != null && File.Exists(_cachedBridgePath))
                {
                    return _cachedBridgePath;
                }

                // 1. Check current application directory
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localExe = Path.Combine(baseDir, "RSBridge.exe");
                if (File.Exists(localExe))
                {
                    _cachedBridgePath = localExe;
                    return _cachedBridgePath;
                }

                // 2. Check local AppData extraction directory
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BimboClub",
                    "RevitServerManager");

                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }

                string appDataExe = Path.Combine(appDataDir, "RSBridge.exe");

                // 3. Extract from EmbeddedResource if present
                var assembly = Assembly.GetExecutingAssembly();
                string[] resNames = assembly.GetManifestResourceNames();
                string? bridgeRes = null;
                foreach (var name in resNames)
                {
                    if (name.EndsWith("RSBridge.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        bridgeRes = name;
                        break;
                    }
                }

                if (bridgeRes != null)
                {
                    try
                    {
                        using var stream = assembly.GetManifestResourceStream(bridgeRes);
                        if (stream != null)
                        {
                            using var fs = new FileStream(appDataExe, FileMode.Create, FileAccess.Write);
                            stream.CopyTo(fs);
                            _cachedBridgePath = appDataExe;
                            return _cachedBridgePath;
                        }
                    }
                    catch { }
                }

                if (File.Exists(appDataExe))
                {
                    _cachedBridgePath = appDataExe;
                    return _cachedBridgePath;
                }

                // 4. Check development path
                string devExe = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "RevitServerBridge", "bin", "Release", "net48", "RSBridge.exe"));
                if (File.Exists(devExe))
                {
                    _cachedBridgePath = devExe;
                    return _cachedBridgePath;
                }

                return null;
            }
        }

        public static async Task<FolderContents?> QueryViaBridgeAsync(string host, string folderPath, string version, CancellationToken ct = default)
        {
            string? bridgeExe = EnsureBridgeExecutable();
            if (bridgeExe == null) return null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = bridgeExe,
                    Arguments = $"\"{host}\" \"{folderPath}\" \"{version}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync(ct);

                if (string.IsNullOrWhiteSpace(output)) return null;

                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                if (root.TryGetProperty("Success", out var succElem) && succElem.GetBoolean())
                {
                    var result = new FolderContents
                    {
                        Folders = new List<FolderObjectData>(),
                        Models = new List<ModelObjectData>()
                    };

                    if (root.TryGetProperty("Folders", out var foldersElem) && foldersElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var f in foldersElem.EnumerateArray())
                        {
                            string fName = f.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(fName))
                            {
                                result.Folders.Add(new FolderObjectData { Name = fName, FolderCount = 1 });
                            }
                        }
                    }

                    if (root.TryGetProperty("Models", out var modelsElem) && modelsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in modelsElem.EnumerateArray())
                        {
                            string mName = m.GetProperty("Name").GetString() ?? "";
                            long size = 0;
                            if (m.TryGetProperty("Size", out var sElem))
                            {
                                size = sElem.GetInt64();
                            }
                            if (!string.IsNullOrWhiteSpace(mName))
                            {
                                result.Models.Add(new ModelObjectData { Name = mName, Size = size });
                            }
                        }
                    }

                    return result;
                }
            }
            catch
            {
                // Fall back to REST on any bridge execution issue
            }

            return null;
        }
    }
}
