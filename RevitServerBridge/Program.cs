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
            Dictionary<string, object> emptySuccessResult = null;

            foreach (var year in years)
            {
                foreach (var pathCandidate in pathsToTry)
                {
                    try
                    {
                        var result = TryConnect(serverHost, pathCandidate, year);
                        if (result != null)
                        {
                            var folders = (List<string>)result["Folders"];
                            var models = (List<Dictionary<string, object>>)result["Models"];

                            if (folders.Count > 0 || models.Count > 0)
                            {
                                Console.WriteLine(new JavaScriptSerializer().Serialize(result));
                                return 0;
                            }

                            // Keep the first working connection as fallback even if empty
                            if (emptySuccessResult == null)
                            {
                                emptySuccessResult = result;
                            }
                        }
                    }
                    catch (EndpointNotFoundException)
                    {
                        break; // This year endpoint doesn't exist, try next year
                    }
                    catch (CommunicationException ex) when (
                        ex.Message.Contains("404") ||
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
            if (emptySuccessResult != null)
            {
                Console.WriteLine(new JavaScriptSerializer().Serialize(emptySuccessResult));
                return 0;
            }

            string errMsg = lastEx?.InnerException?.Message ?? lastEx?.Message ?? "Failed to connect to Revit Server";
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
                list.Add("\\");
                list.Add("/");
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
            list.Add(string.Join("/", parts) + "/");
            list.Add(string.Join("/", parts));
            if (!list.Contains(inputPath)) list.Add(inputPath);

            return list;
        }

        static bool IsDnsOrSocketError(Exception ex)
        {
            var msg = ex.Message + (ex.InnerException?.Message ?? "");
            return msg.Contains("DNS") || msg.Contains("No such host") ||
                   msg.Contains("SocketException") ||
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

                            string machine = Environment.MachineName;
                            string userName = "RevitServerTool:" + machine + ":1";
                            var token = new ServiceSessionToken(
                                userName,
                                userName,
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
                            catch { }

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
                SendTimeout = TimeSpan.FromMinutes(2),
                ReceiveTimeout = TimeSpan.FromMinutes(5),
                OpenTimeout = TimeSpan.FromSeconds(20),
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
