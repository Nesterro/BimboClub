using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.DataContract.SessionToken;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.Proxy;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model;

namespace RevitServerBridge
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{\"Success\":false,\"Error\":\"Usage: RSBridge.exe <ServerHost> [FolderPath]\"}");
                return 1;
            }

            string serverHost = args[0].Trim().Replace("http://", "").Replace("https://", "").Trim('/');
            string folderPath = args.Length > 1 ? args[1] : "|";
            if (string.IsNullOrWhiteSpace(folderPath)) folderPath = "|";

            try
            {
                var proxyProvider = ProxyProvider.Instance;
                var proxy = proxyProvider.GetBufferedProxy<IModelService>(serverHost);

                var token = new ServiceSessionToken(
                    Environment.UserName,
                    Environment.UserName,
                    Environment.MachineName,
                    Guid.NewGuid().ToString()
                );

                var folders = new List<string>();
                var models = new List<Dictionary<string, object>>();

                ArrayList subFolders = null;
                ArrayList modelList = null;

                bool querySuccess = false;
                try
                {
                    querySuccess = proxy.Proxy.ListSubFoldersAndModels(token, folderPath, out subFolders, out modelList);
                }
                catch { }

                if (subFolders != null)
                {
                    foreach (var f in subFolders)
                    {
                        if (f != null)
                        {
                            string fName = f.ToString().Trim();
                            if (!string.IsNullOrEmpty(fName)) folders.Add(fName);
                        }
                    }
                }

                if (modelList != null)
                {
                    foreach (var m in modelList)
                    {
                        if (m != null)
                        {
                            string mName = m.ToString().Trim();
                            if (!string.IsNullOrEmpty(mName))
                            {
                                models.Add(new Dictionary<string, object>
                                {
                                    { "Name", mName },
                                    { "Size", 0L }
                                });
                            }
                        }
                    }
                }

                // Fallback to GetListOfModelFilesAndFolders if nothing returned
                if (folders.Count == 0 && models.Count == 0)
                {
                    try
                    {
                        List<string> filesOut = null;
                        List<string> foldersOut = null;
                        var valStatus = proxy.Proxy.GetListOfModelFilesAndFolders(token, folderPath, out filesOut, out foldersOut);

                        if (foldersOut != null)
                        {
                            foreach (var f in foldersOut)
                            {
                                if (!string.IsNullOrWhiteSpace(f) && !folders.Contains(f.Trim()))
                                {
                                    folders.Add(f.Trim());
                                }
                            }
                        }

                        if (filesOut != null)
                        {
                            foreach (var f in filesOut)
                            {
                                if (!string.IsNullOrWhiteSpace(f))
                                {
                                    string fName = f.Trim();
                                    models.Add(new Dictionary<string, object>
                                    {
                                        { "Name", fName },
                                        { "Size", 0L }
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }

                var res = new Dictionary<string, object>
                {
                    { "Success", true },
                    { "Host", serverHost },
                    { "Path", folderPath },
                    { "Folders", folders },
                    { "Models", models }
                };

                var serializer = new JavaScriptSerializer();
                Console.WriteLine(serializer.Serialize(res));
                return 0;
            }
            catch (Exception ex)
            {
                string errMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                var errObj = new Dictionary<string, object>
                {
                    { "Success", false },
                    { "Error", errMsg },
                    { "Details", ex.ToString() }
                };
                var serializer = new JavaScriptSerializer();
                Console.WriteLine(serializer.Serialize(errObj));
                return 3;
            }
        }
    }
}
