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

        // Root folder path candidates: empty string is the most likely for Revit Server root
        static readonly string[] RootPathCandidates = { "", "|", "/", "\\" };

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{\"Success\":false,\"Error\":\"Usage: RSBridge.exe <ServerHost> [FolderPath] [Version]\"}");
                return 1;
            }

            string serverHost = args[0].Trim().Replace("http://", "").Replace("https://", "").Trim('/');
            string folderPath = args.Length > 1 ? args[1] : "";
            string preferredYear = args.Length > 2 ? args[2].Trim() : "2022";

            // For root requests, try all root path candidates
            bool isRootRequest = folderPath == "|" || folderPath == "" || folderPath == "/";

            var years = new List<string> { preferredYear };
            foreach (var y in YearOrder)
                if (!years.Contains(y)) years.Add(y);

            Exception lastEx = null;

            foreach (var year in years)
            {
                // Determine which paths to try for this request
                var pathsToTry = isRootRequest
                    ? RootPathCandidates
                    : new[] { folderPath };

                foreach (var pathCandidate in pathsToTry)
                {
                    try
                    {
                        var result = TryConnect(serverHost, pathCandidate, year);
                        if (result != null)
                        {
                            // Only accept root result if it has content OR all candidates are exhausted
                            bool hasContent = result.ContainsKey("Folders") &&
                                             ((List<string>)result["Folders"]).Count > 0;
                            bool hasModels = result.ContainsKey("Models") &&
                                            ((List<Dictionary<string, object>>)result["Models"]).Count > 0;

                            if (!isRootRequest || hasContent || hasModels || pathCandidate == pathsToTry[pathsToTry.Length - 1])
                            {
                                // Convert to serializable format
                                var output = new Dictionary<string, object>
                                {
                                    { "Success", true },
                                    { "Host", serverHost },
                                    { "Path", pathCandidate },
                                    { "Year", year },
                                    { "Endpoint", result["Endpoint"] },
                                    { "Auth", result["Auth"] },
                                    { "Folders", result["Folders"] },
                                    { "Models", result["Models"] }
                                };
                                Console.WriteLine(new JavaScriptSerializer().Serialize(output));
                                return 0;
                            }
                            // Root returned empty - try next path candidate
                        }
                    }
                    catch (EndpointNotFoundException)
                    {
                        break; // This year doesn't have an endpoint - skip all paths, try next year
                    }
                    catch (CommunicationException ex) when (
                        ex.Message.Contains("404") ||
                        ex.Message.Contains("not found") ||
                        ex.Message.Contains("rejected") ||
                        ex.Message.Contains("ReadingUpgradeRecord"))
                    {
                        lastEx = ex;
                        break; // Try next year
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
            string errMsg = lastEx?.InnerException?.Message ?? lastEx?.Message ?? "Failed to connect to Revit Server";
            Console.WriteLine(new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "Success", false },
                { "Error", errMsg },
                { "Details", lastEx?.ToString() ?? "" }
            }));
            return 3;
        }

        static bool IsDnsOrSocketError(Exception ex)
        {
            var msg = ex.Message + (ex.InnerException?.Message ?? "");
            return msg.Contains("DNS") || msg.Contains("No such host") ||
                   msg.Contains("SocketException") || msg.Contains("actively refused") ||
                   ex.InnerException is System.Net.Sockets.SocketException;
        }

        static Dictionary<string, object> TryConnect(string serverHost, string folderPath, string year)
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

                            var folders = new List<string>();
                            var models = new List<Dictionary<string, object>>();

                            // Method 1: ListSubFoldersAndModels
                            // relativeFolderPath: "" for root, or subfolder path
                            ArrayList subFolders = null;
                            ArrayList modelList = null;
                            try
                            {
                                channel.ListSubFoldersAndModels(token, folderPath, out subFolders, out modelList);
                            }
                            catch { }

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

                            // Method 2: GetListOfModelFilesAndFolders (uses List<string>)
                            // Only use as fallback if Method 1 returned nothing
                            if (folders.Count == 0 && models.Count == 0)
                            {
                                try
                                {
                                    List<string> filesOut = null;
                                    List<string> foldersOut = null;
                                    channel.GetListOfModelFilesAndFolders(token, folderPath, out filesOut, out foldersOut);
                                    if (foldersOut != null)
                                        foreach (var f in foldersOut)
                                        {
                                            string fn = f?.Trim();
                                            if (!string.IsNullOrWhiteSpace(fn) && !folders.Contains(fn))
                                                folders.Add(fn);
                                        }
                                    if (filesOut != null)
                                        foreach (var f in filesOut)
                                        {
                                            string fn = f?.Trim();
                                            if (!string.IsNullOrWhiteSpace(fn))
                                                models.Add(new Dictionary<string, object> { { "Name", fn }, { "Size", 0L } });
                                        }
                                }
                                catch { }
                            }

                            return new Dictionary<string, object>
                            {
                                { "Success", true },
                                { "Host", serverHost },
                                { "Path", folderPath },
                                { "Year", year },
                                { "Endpoint", endpointUrl },
                                { "Auth", useWindowsAuth ? "Windows" : "None" },
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
                        ex.Message.Contains("rejected") ||
                        ex.Message.Contains("404"))
                    {
                        continue;
                    }
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

            // Try to get Name property via reflection (for data contract objects)
            var type = item.GetType();
            var nameProp = type.GetProperty("Name") ?? type.GetProperty("FolderName")
                ?? type.GetProperty("ModelName") ?? type.GetProperty("Path")
                ?? type.GetProperty("RelativePath");
            if (nameProp != null)
            {
                var val = nameProp.GetValue(item)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // Last resort: ToString() but skip type names
            string str = item.ToString();
            if (str.Contains(".") && str.Contains("Common")) return null; // Looks like a type name
            return str.Trim();
        }

        static NetTcpBinding CreateBinding(bool windowsAuth = false)
        {
            var binding = new NetTcpBinding
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
                binding.Security.Mode = SecurityMode.Transport;
                binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            }
            else
            {
                binding.Security.Mode = SecurityMode.None;
            }

            return binding;
        }
    }
}
