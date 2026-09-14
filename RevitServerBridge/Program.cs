using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Web.Script.Serialization;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.DataContract.SessionToken;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model;

namespace RevitServerBridge
{
    class Program
    {
        static readonly string[] YearOrder = { "2022", "2021", "2023", "2020", "2024", "2019", "2018", "2025" };

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{\"Success\":false,\"Error\":\"Usage: RSBridge.exe <ServerHost> [FolderPath] [Version]\"}");
                return 1;
            }

            string serverHost = args[0].Trim().Replace("http://", "").Replace("https://", "").Trim('/');
            string inputPath = "";
            string preferredYear = "2022";

            if (args.Length == 2)
            {
                string a1 = args[1].Trim();
                if (a1.Length == 4 && a1.StartsWith("20") && int.TryParse(a1, out _))
                {
                    preferredYear = a1;
                    inputPath = "";
                }
                else
                {
                    inputPath = a1;
                }
            }
            else if (args.Length >= 3)
            {
                inputPath = args[1].Trim();
                preferredYear = args[2].Trim();
            }

            var years = new List<string> { preferredYear };
            foreach (var y in YearOrder)
                if (!years.Contains(y)) years.Add(y);

            var pathsToTry = GetWcfPathsToTry(inputPath);

            Exception lastEx = null;
            Dictionary<string, object> workingEmptyResult = null;

            foreach (var year in years)
            {
                foreach (var pathCandidate in pathsToTry)
                {
                    try
                    {
                        var result = TryConnect(serverHost, pathCandidate, year, out Exception connEx);
                        if (connEx != null) lastEx = connEx;

                        if (result != null)
                        {
                            var folders = (List<string>)result["Folders"];
                            var models = (List<Dictionary<string, object>>)result["Models"];

                            if (folders.Count > 0 || models.Count > 0)
                            {
                                Console.WriteLine(new JavaScriptSerializer().Serialize(result));
                                return 0;
                            }

                            if (workingEmptyResult == null)
                            {
                                workingEmptyResult = result;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                    }

                    if (IsDnsOrSocketError(lastEx))
                    {
                        // Network/DNS down (e.g. VPN off), no point in trying other years
                        goto done;
                    }
                }

                // If this year connected successfully (even if empty), don't try older Revit versions!
                if (workingEmptyResult != null)
                {
                    break;
                }
            }

            done:
            if (workingEmptyResult != null)
            {
                Console.WriteLine(new JavaScriptSerializer().Serialize(workingEmptyResult));
                return 0;
            }

            string errMsg = "Не удалось подключиться к Revit Server";
            if (lastEx != null)
            {
                errMsg = lastEx.InnerException?.Message ?? lastEx.Message;
            }
            if (IsDnsOrSocketError(lastEx))
            {
                errMsg = $"Хост {serverHost} недоступен (проверьте подключение к корпоративному VPN). Ошибка сети: {errMsg}";
            }

            Console.WriteLine(new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "Success", false },
                { "Error", errMsg },
                { "Details", lastEx?.ToString() ?? "" }
            }));
            return 3;
        }

        static List<string> GetWcfPathsToTry(string inputPath)
        {
            var list = new List<string>();

            if (string.IsNullOrWhiteSpace(inputPath) || inputPath == "|" || inputPath == "/" || inputPath == "\\")
            {
                list.Add("");
                list.Add("|");
                return list;
            }

            var parts = inputPath.Split(new[] { '|', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                list.Add("");
                return list;
            }

            list.Add(string.Join("\\", parts) + "\\");
            list.Add(string.Join("\\", parts));
            list.Add(inputPath);

            return list;
        }

        static bool IsDnsOrSocketError(Exception ex)
        {
            if (ex == null) return false;
            if (ex is System.Net.Sockets.SocketException || ex.InnerException is System.Net.Sockets.SocketException)
                return true;

            string msg = (ex.Message + " " + (ex.InnerException?.Message ?? "")).ToLowerInvariant();
            return msg.Contains("dns") ||
                   msg.Contains("host") ||
                   msg.Contains("неизвестен") ||
                   msg.Contains("не удается разрешить") ||
                   msg.Contains("socket") ||
                   msg.Contains("refused") ||
                   msg.Contains("отверг") ||
                   msg.Contains("timed out") ||
                   msg.Contains("время ожидания");
        }

        static Dictionary<string, object> TryConnect(string serverHost, string folderPath, string year, out Exception lastException)
        {
            lastException = null;

            // Try anonymous (SecurityMode.None) first as standard Autodesk Revit Server uses it,
            // then Windows Auth (Transport security) as fallback
            bool[] authModes = { false, true };
            string[] bindingSuffixes = { "tcpbuffer", "tcpstreamed", "" };

            // Try both username styles: standard Windows username and Autodesk tool username
            string machine = Environment.MachineName;
            string[] userNamesToTry = {
                Environment.UserName,
                "RevitServerTool:" + machine + ":1"
            };

            foreach (var useWindowsAuth in authModes)
            {
                foreach (var suffix in bindingSuffixes)
                {
                    string svcPath = string.IsNullOrEmpty(suffix)
                        ? $"ModelService{year}/ModelService.svc"
                        : $"ModelService{year}/ModelService.svc/{suffix}";
                    string endpointUrl = $"net.tcp://{serverHost}/{svcPath}";

                    IModelService channel = null;
                    ChannelFactory<IModelService> factory = null;

                    try
                    {
                        var binding = CreateBinding(windowsAuth: useWindowsAuth);
                        factory = new ChannelFactory<IModelService>(binding, new EndpointAddress(endpointUrl));
                        channel = factory.CreateChannel();
                        ((IClientChannel)channel).Open(TimeSpan.FromSeconds(8));

                        // Channel is open! Now try listing folders
                        foreach (var uName in userNamesToTry)
                        {
                            var token = new ServiceSessionToken(
                                uName,
                                uName,
                                machine,
                                Guid.NewGuid().ToString()
                            );

                            var folders = new List<string>();
                            var models = new List<Dictionary<string, object>>();

                            // Method 1: ListSubFoldersAndModels
                            ArrayList subFolders = null;
                            ArrayList modelList = null;
                            try
                            {
                                channel.ListSubFoldersAndModels(token, folderPath, out subFolders, out modelList);
                            }
                            catch (Exception ex)
                            {
                                lastException = ex;
                            }

                            if (subFolders != null)
                            {
                                foreach (var f in subFolders)
                                {
                                    ParseItem(f, folders);
                                }
                            }

                            if (modelList != null)
                            {
                                foreach (var m in modelList)
                                {
                                    ParseModel(m, models);
                                }
                            }

                            // Method 2: GetListOfModelFilesAndFolders fallback
                            if (folders.Count == 0 && models.Count == 0)
                            {
                                try
                                {
                                    List<string> filesOut = null;
                                    List<string> foldersOut = null;
                                    channel.GetListOfModelFilesAndFolders(token, folderPath, out filesOut, out foldersOut);

                                    if (foldersOut != null)
                                    {
                                        foreach (var f in foldersOut)
                                        {
                                            ParseItem(f, folders);
                                        }
                                    }

                                    if (filesOut != null)
                                    {
                                        foreach (var f in filesOut)
                                        {
                                            ParseModel(f, models);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    lastException = ex;
                                }
                            }

                            if (folders.Count > 0 || models.Count > 0)
                            {
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
                        }

                        // Connected successfully even if no contents
                        return new Dictionary<string, object>
                        {
                            { "Success", true },
                            { "Host", serverHost },
                            { "Path", folderPath },
                            { "Year", year },
                            { "Endpoint", endpointUrl },
                            { "Auth", useWindowsAuth ? "Windows" : "None" },
                            { "Folders", new List<string>() },
                            { "Models", new List<Dictionary<string, object>>() }
                        };
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        if (IsDnsOrSocketError(ex))
                        {
                            return null;
                        }
                    }
                    finally
                    {
                        try { ((IClientChannel)channel)?.Close(TimeSpan.FromSeconds(2)); } catch { }
                        try { factory?.Close(TimeSpan.FromSeconds(2)); } catch { }
                    }
                }
            }

            return null;
        }

        static void ParseItem(object item, List<string> list)
        {
            if (item == null) return;
            string s = item.ToString();
            if (string.IsNullOrWhiteSpace(s)) return;

            string[] parts = s.Split('|');
            string name = parts.Length > 0 ? parts[0].Trim() : s.Trim();
            if (!string.IsNullOrEmpty(name) && !list.Contains(name))
            {
                list.Add(name);
            }
        }

        static void ParseModel(object item, List<Dictionary<string, object>> list)
        {
            if (item == null) return;
            string s = item.ToString();
            if (string.IsNullOrWhiteSpace(s)) return;

            string[] parts = s.Split('|');
            string name = parts.Length > 0 ? parts[0].Trim() : s.Trim();
            long size = 0;
            if (parts.Length > 2)
            {
                long.TryParse(parts[2].Trim(), out size);
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (!list.Any(m => (string)m["Name"] == name))
                {
                    list.Add(new Dictionary<string, object>
                    {
                        { "Name", name },
                        { "Size", size }
                    });
                }
            }
        }

        static NetTcpBinding CreateBinding(bool windowsAuth = false)
        {
            var b = new NetTcpBinding
            {
                TransferMode = TransferMode.Buffered,
                MaxReceivedMessageSize = 67108864L,
                MaxBufferSize = 67108864,
                MaxBufferPoolSize = 67108864L,
                SendTimeout = TimeSpan.FromSeconds(15),
                ReceiveTimeout = TimeSpan.FromMinutes(2),
                OpenTimeout = TimeSpan.FromSeconds(8),
                CloseTimeout = TimeSpan.FromSeconds(5),
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
