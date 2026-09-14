using System;
using System.Collections;
using System.Collections.Generic;
using System.ServiceModel;
using System.Web.Script.Serialization;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.DataContract.SessionToken;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model;

namespace RevitServerBridge
{
    class Program
    {
        static readonly string[] YearOrder = { "2022", "2021", "2023", "2020", "2024", "2019", "2018", "2025" };
        // Root path candidates - try all of these for root listing
        static readonly string[] RootPaths = { "", "|", "/", "\\", "root", " " };

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{\"Success\":false,\"Error\":\"Usage: RSBridge.exe <ServerHost> [FolderPath] [Version]\"}");
                return 1;
            }

            string serverHost = args[0].Trim().Replace("http://", "").Replace("https://", "").Trim('/');
            // Normalize: treat "2022", "2021" etc passed as path as version numbers (user mistake)
            string rawPath = args.Length > 1 ? args[1] : "";
            string preferredYear = args.Length > 2 ? args[2].Trim() : "2022";

            // If rawPath looks like a year, it was probably passed as version by mistake
            bool rawPathIsYear = rawPath.Length == 4 && rawPath.StartsWith("20") && int.TryParse(rawPath, out _);
            if (rawPathIsYear && args.Length == 2)
            {
                preferredYear = rawPath;
                rawPath = "";
            }

            string folderPath = rawPath;
            bool isRootRequest = string.IsNullOrEmpty(folderPath) || folderPath == "|" || folderPath == "/" || folderPath == "\\";

            var years = new List<string> { preferredYear };
            foreach (var y in YearOrder)
                if (!years.Contains(y)) years.Add(y);

            Exception lastEx = null;
            var diagnostics = new List<string>();

            foreach (var year in years)
            {
                var pathsToTry = isRootRequest ? RootPaths : new[] { folderPath };

                foreach (var pathCandidate in pathsToTry)
                {
                    try
                    {
                        var result = TryConnect(serverHost, pathCandidate, year, diagnostics);
                        if (result != null)
                        {
                            var folders = (List<string>)result["Folders"];
                            var models = (List<Dictionary<string, object>>)result["Models"];

                            bool hasContent = folders.Count > 0 || models.Count > 0;

                            // Accept result if: has content, not root request, or last candidate
                            if (!isRootRequest || hasContent || pathCandidate == RootPaths[RootPaths.Length - 1])
                            {
                                var output = new Dictionary<string, object>
                                {
                                    { "Success", true },
                                    { "Host", serverHost },
                                    { "Path", pathCandidate },
                                    { "Year", year },
                                    { "Endpoint", result["Endpoint"] },
                                    { "Auth", result["Auth"] },
                                    { "IsHostNode", result["IsHostNode"] },
                                    { "MaxPathLen", result["MaxPathLen"] },
                                    { "RawSubFoldersNull", result["RawSubFoldersNull"] },
                                    { "RawModelsNull", result["RawModelsNull"] },
                                    { "Folders", folders },
                                    { "Models", models },
                                    { "Diagnostics", diagnostics }
                                };
                                Console.WriteLine(new JavaScriptSerializer().Serialize(output));
                                return 0;
                            }
                            // Empty root result, try next path
                            diagnostics.Add($"Path '{pathCandidate}' returned empty (year={year})");
                        }
                    }
                    catch (EndpointNotFoundException)
                    {
                        diagnostics.Add($"EndpointNotFound for year={year}");
                        break;
                    }
                    catch (CommunicationException ex) when (
                        ex.Message.Contains("404") ||
                        ex.Message.Contains("rejected") ||
                        ex.Message.Contains("ReadingUpgradeRecord"))
                    {
                        lastEx = ex;
                        diagnostics.Add($"CommEx for year={year}: {ex.Message.Substring(0, Math.Min(80, ex.Message.Length))}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        if (IsDnsOrSocketError(ex)) goto done;
                        break;
                    }
                }
            }

            done:
            string errMsg = lastEx?.InnerException?.Message ?? lastEx?.Message ?? "Failed to connect";
            Console.WriteLine(new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "Success", false },
                { "Error", errMsg },
                { "Diagnostics", diagnostics },
                { "Details", lastEx?.ToString() ?? "" }
            }));
            return 3;
        }

        static bool IsDnsOrSocketError(Exception ex)
        {
            var msg = ex.Message + (ex.InnerException?.Message ?? "");
            return msg.Contains("DNS") || msg.Contains("No such host") ||
                   msg.Contains("SocketException") ||
                   ex.InnerException is System.Net.Sockets.SocketException;
        }

        static Dictionary<string, object> TryConnect(string serverHost, string folderPath, string year, List<string> diagnostics)
        {
            string[] bindingSuffixes = { "tcpbuffer", "tcpstreamed", "" };
            bool[] authModes = { true, false };

            foreach (var useWindowsAuth in authModes)
            {
                foreach (var suffix in bindingSuffixes)
                {
                    string svcPath = string.IsNullOrEmpty(suffix)
                        ? $"ModelService{year}/ModelService.svc"
                        : $"ModelService{year}/ModelService.svc/{suffix}";
                    string endpointUrl = $"net.tcp://{serverHost}/{svcPath}";

                    try
                    {
                        var binding = CreateBinding(windowsAuth: useWindowsAuth);
                        var factory = new ChannelFactory<IModelService>(binding, new EndpointAddress(endpointUrl));

                        IModelService channel = null;
                        try
                        {
                            channel = factory.CreateChannel();
                            ((IClientChannel)channel).Open(TimeSpan.FromSeconds(12));

                            var token = new ServiceSessionToken(
                                Environment.UserName,
                                Environment.UserName,
                                Environment.MachineName,
                                Guid.NewGuid().ToString()
                            );

                            // Diagnostic: verify basic WCF calls work
                            bool isHostNode = false;
                            int maxPathLen = -1;
                            try { isHostNode = channel.IsHostNode(); } catch { }
                            try { maxPathLen = channel.GetMaxModelPathLength(token); } catch { }

                            diagnostics.Add($"Connected: endpoint={endpointUrl}, auth={useWindowsAuth}, IsHostNode={isHostNode}, MaxPathLen={maxPathLen}");

                            var folders = new List<string>();
                            var models = new List<Dictionary<string, object>>();

                            // Method 1: ListSubFoldersAndModels
                            ArrayList subFolders = null;
                            ArrayList modelList = null;
                            bool querySuccess = false;
                            try
                            {
                                querySuccess = channel.ListSubFoldersAndModels(token, folderPath, out subFolders, out modelList);
                                diagnostics.Add($"ListSubFoldersAndModels('{folderPath}'): success={querySuccess}, subFolders={subFolders?.Count.ToString() ?? "null"}, models={modelList?.Count.ToString() ?? "null"}");
                            }
                            catch (Exception ex)
                            {
                                diagnostics.Add($"ListSubFoldersAndModels EXCEPTION: {ex.Message.Substring(0, Math.Min(120, ex.Message.Length))}");
                            }

                            bool rawSubNull = subFolders == null;
                            bool rawModNull = modelList == null;

                            if (subFolders != null)
                                foreach (var f in subFolders)
                                {
                                    string fn = ExtractName(f);
                                    if (!string.IsNullOrEmpty(fn) && !folders.Contains(fn))
                                        folders.Add(fn);
                                }
                            if (modelList != null)
                                foreach (var m in modelList)
                                {
                                    string mn = ExtractName(m);
                                    if (!string.IsNullOrEmpty(mn))
                                        models.Add(new Dictionary<string, object> { { "Name", mn }, { "Size", 0L } });
                                }

                            // Method 2: GetListOfModelFilesAndFolders fallback
                            if (folders.Count == 0 && models.Count == 0)
                            {
                                try
                                {
                                    List<string> filesOut = null;
                                    List<string> foldersOut = null;
                                    var status = channel.GetListOfModelFilesAndFolders(token, folderPath, out filesOut, out foldersOut);
                                    diagnostics.Add($"GetListOfModelFilesAndFolders('{folderPath}'): status={status}, files={filesOut?.Count.ToString() ?? "null"}, folders={foldersOut?.Count.ToString() ?? "null"}");
                                    if (foldersOut != null)
                                        foreach (var f in foldersOut)
                                        {
                                            if (!string.IsNullOrWhiteSpace(f) && !folders.Contains(f.Trim()))
                                                folders.Add(f.Trim());
                                        }
                                    if (filesOut != null)
                                        foreach (var f in filesOut)
                                        {
                                            if (!string.IsNullOrWhiteSpace(f))
                                                models.Add(new Dictionary<string, object> { { "Name", f.Trim() }, { "Size", 0L } });
                                        }
                                }
                                catch (Exception ex)
                                {
                                    diagnostics.Add($"GetListOfModelFilesAndFolders EXCEPTION: {ex.Message.Substring(0, Math.Min(120, ex.Message.Length))}");
                                }
                            }

                            return new Dictionary<string, object>
                            {
                                { "Success", true },
                                { "Host", serverHost },
                                { "Path", folderPath },
                                { "Year", year },
                                { "Endpoint", endpointUrl },
                                { "Auth", useWindowsAuth ? "Windows" : "None" },
                                { "IsHostNode", isHostNode },
                                { "MaxPathLen", maxPathLen },
                                { "RawSubFoldersNull", rawSubNull },
                                { "RawModelsNull", rawModNull },
                                { "Folders", folders },
                                { "Models", models }
                            };
                        }
                        finally
                        {
                            try { ((IClientChannel)channel)?.Close(TimeSpan.FromSeconds(3)); } catch { }
                            try { factory.Close(TimeSpan.FromSeconds(3)); } catch { }
                        }
                    }
                    catch (EndpointNotFoundException) { throw; }
                    catch (CommunicationException ex) when (
                        ex.Message.Contains("ReadingUpgradeRecord") ||
                        ex.Message.Contains("rejected"))
                    { continue; }
                    catch (CommunicationException) { throw; }
                    catch (Exception) { throw; }
                }
            }
            return null;
        }

        static string ExtractName(object item)
        {
            if (item == null) return null;
            if (item is string s) return s.Trim();
            var type = item.GetType();
            foreach (var pname in new[] { "Name", "FolderName", "ModelName", "Path", "RelativePath", "Value" })
            {
                var prop = type.GetProperty(pname);
                if (prop != null)
                {
                    var val = prop.GetValue(item)?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }
            string str = item.ToString();
            return (str.Contains(".") && str.Contains("Common")) ? null : str.Trim();
        }

        static NetTcpBinding CreateBinding(bool windowsAuth = false)
        {
            var b = new NetTcpBinding
            {
                TransferMode = TransferMode.Buffered,
                MaxReceivedMessageSize = 67108864L,
                MaxBufferSize = 67108864,
                MaxBufferPoolSize = 67108864L,
                SendTimeout = TimeSpan.FromSeconds(30),
                ReceiveTimeout = TimeSpan.FromMinutes(5),
                OpenTimeout = TimeSpan.FromSeconds(15),
                CloseTimeout = TimeSpan.FromSeconds(10),
                ReaderQuotas = new System.Xml.XmlDictionaryReaderQuotas
                {
                    MaxArrayLength = 67108864,
                    MaxBytesPerRead = 67108864,
                    MaxDepth = 32,
                    MaxNameTableCharCount = 67108864,
                    MaxStringContentLength = 67108864
                }
            };
            if (windowsAuth)
            {
                b.Security.Mode = SecurityMode.Transport;
                b.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            }
            else
            {
                b.Security.Mode = SecurityMode.None;
            }
            return b;
        }
    }
}
