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

        private static readonly string[] RequiredFiles = new[]
        {
            "RSBridge.exe",
            "Castle.Core.dll",
            "Castle.Windsor.dll",
            "Autodesk.RevitServer.Social.dll",
            "RS.Enterprise.Common.ClientServer.DataContract.dll",
            "RS.Enterprise.Common.ClientServer.Helper.dll",
            "RS.Enterprise.Common.ClientServer.Proxy.dll",
            "RS.Enterprise.Common.ClientServer.ServiceContract.Local.dll",
            "RS.Enterprise.Common.ClientServer.ServiceContract.Model.dll"
        };

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
                if (File.Exists(localExe) && File.Exists(Path.Combine(baseDir, "RS.Enterprise.Common.ClientServer.Proxy.dll")))
                {
                    _cachedBridgePath = localExe;
                    return _cachedBridgePath;
                }

                // 2. Check local AppData extraction directory
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BimboClub",
                    "RevitServerManager",
                    "Bridge");

                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }

                string appDataExe = Path.Combine(appDataDir, "RSBridge.exe");

                // 3. Extract embedded files if needed
                var assembly = Assembly.GetExecutingAssembly();
                var resNames = assembly.GetManifestResourceNames();

                foreach (var k in RequiredFiles)
                {
                    string targetFile = Path.Combine(appDataDir, k);
                    foreach (var res in resNames)
                    {
                        if (res.EndsWith(k, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using var stream = assembly.GetManifestResourceStream(res);
                                if (stream != null)
                                {
                                    bool needsUpdate = !File.Exists(targetFile) ||
                                                       new FileInfo(targetFile).Length != stream.Length;

                                    if (needsUpdate)
                                    {
                                        using var fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                                        stream.CopyTo(fs);
                                    }
                                }
                            }
                            catch { }
                            break;
                        }
                    }
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
                    WorkingDirectory = Path.GetDirectoryName(bridgeExe) ?? "",
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
                // Fall back to REST on bridge failure
            }

            return null;
        }
    }
}
