using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace RevitServerBridge
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{\"Success\":false,\"Error\":\"Usage: RSBridge.exe <ServerHost> [FolderPath] [RevitVersion]\"}");
                return 1;
            }

            string serverHost = args[0].Trim().Replace("http://", "").Replace("https://", "").Trim('/');
            string folderPath = args.Length > 1 ? args[1] : "|";
            string preferredVer = args.Length > 2 ? args[2] : "2022";

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                folderPath = "|";
            }

            string[] possibleYears = new string[]
            {
                preferredVer,
                "2022", "2021", "2020", "2023", "2024", "2025", "2026", "2019", "2018"
            };

            string rstDir = null;
            foreach (string y in possibleYears)
            {
                string d = @"C:\Program Files\Autodesk\Revit " + y + @"\RevitServerToolCommand";
                if (Directory.Exists(d) && File.Exists(Path.Combine(d, "RS.Enterprise.Common.ClientServer.Proxy.dll")))
                {
                    rstDir = d;
                    break;
                }
            }

            if (rstDir == null)
            {
                Console.WriteLine("{\"Success\":false,\"Error\":\"Директория RevitServerToolCommand не найдена ни для одной версии Revit.\"}");
                return 2;
            }

            AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs ev)
            {
                string dllName = new AssemblyName(ev.Name).Name + ".dll";
                string fullPath = Path.Combine(rstDir, dllName);
                if (File.Exists(fullPath))
                {
                    return Assembly.LoadFrom(fullPath);
                }
                return null;
            };

            try
            {
                // Preload required assemblies
                LoadIfExists(Path.Combine(rstDir, "Castle.Core.dll"));
                LoadIfExists(Path.Combine(rstDir, "Castle.Windsor.dll"));
                LoadIfExists(Path.Combine(rstDir, "RS.Enterprise.Common.ClientServer.Helper.dll"));
                LoadIfExists(Path.Combine(rstDir, "RS.Enterprise.Common.ClientServer.DataContract.dll"));
                LoadIfExists(Path.Combine(rstDir, "RS.Enterprise.Common.ClientServer.ServiceContract.Model.dll"));
                LoadIfExists(Path.Combine(rstDir, "RS.Enterprise.Common.ClientServer.Proxy.dll"));

                object result = QueryServer(serverHost, folderPath);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Console.WriteLine(serializer.Serialize(result));
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
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Console.WriteLine(serializer.Serialize(errObj));
                return 3;
            }
        }

        static void LoadIfExists(string path)
        {
            if (File.Exists(path))
            {
                Assembly.LoadFrom(path);
            }
        }

        static object QueryServer(string serverHost, string folderPath)
        {
            Type proxyProviderType = Type.GetType("Autodesk.RevitServer.Enterprise.Common.ClientServer.Proxy.ProxyProvider, RS.Enterprise.Common.ClientServer.Proxy");
            PropertyInfo propInstance = proxyProviderType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            object proxyProvider = propInstance.GetValue(null, null);

            Type modelServiceType = Type.GetType("Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model.IModelService, RS.Enterprise.Common.ClientServer.ServiceContract.Model");

            MethodInfo getBufferedProxyMethod = null;
            foreach (MethodInfo m in proxyProviderType.GetMethods())
            {
                if (m.Name == "GetBufferedProxy" && m.IsGenericMethod && m.GetParameters().Length == 1)
                {
                    getBufferedProxyMethod = m;
                    break;
                }
            }

            MethodInfo genericMethod = getBufferedProxyMethod.MakeGenericMethod(modelServiceType);
            object proxy = genericMethod.Invoke(proxyProvider, new object[] { serverHost });

            PropertyInfo propService = proxy.GetType().GetProperty("Service");
            object service = propService.GetValue(proxy, null);

            // 1. Try ListSubFoldersAndModels
            object listResult = null;
            try
            {
                MethodInfo methodList = modelServiceType.GetMethod("ListSubFoldersAndModels", new Type[] { typeof(string) });
                if (methodList != null)
                {
                    listResult = methodList.Invoke(service, new object[] { folderPath });
                }
            }
            catch { }

            // 2. Fallback to GetListOfModelFilesAndFolders
            if (listResult == null)
            {
                try
                {
                    MethodInfo methodGetList = modelServiceType.GetMethod("GetListOfModelFilesAndFolders", new Type[] { typeof(string) });
                    if (methodGetList != null)
                    {
                        listResult = methodGetList.Invoke(service, new object[] { folderPath });
                    }
                }
                catch { }
            }

            List<string> folders = new List<string>();
            List<Dictionary<string, object>> models = new List<Dictionary<string, object>>();

            if (listResult != null)
            {
                PropertyInfo propFolders = listResult.GetType().GetProperty("Folders");
                if (propFolders != null)
                {
                    IEnumerable fList = propFolders.GetValue(listResult, null) as IEnumerable;
                    if (fList != null)
                    {
                        foreach (object f in fList)
                        {
                            if (f != null)
                            {
                                string fName = f.ToString();
                                if (!string.IsNullOrWhiteSpace(fName))
                                {
                                    folders.Add(fName);
                                }
                            }
                        }
                    }
                }

                PropertyInfo propModels = listResult.GetType().GetProperty("Models");
                if (propModels != null)
                {
                    IEnumerable mList = propModels.GetValue(listResult, null) as IEnumerable;
                    if (mList != null)
                    {
                        foreach (object m in mList)
                        {
                            if (m == null) continue;
                            Dictionary<string, object> modelDict = new Dictionary<string, object>();
                            PropertyInfo pName = m.GetType().GetProperty("ModelName") ?? m.GetType().GetProperty("Name");
                            string name = pName != null ? (pName.GetValue(m, null) ?? "").ToString() : m.ToString();

                            long size = 0;
                            PropertyInfo pSize = m.GetType().GetProperty("ModelSize") ?? m.GetType().GetProperty("Size");
                            if (pSize != null)
                            {
                                object sVal = pSize.GetValue(m, null);
                                if (sVal != null) long.TryParse(sVal.ToString(), out size);
                            }

                            modelDict["Name"] = name;
                            modelDict["Size"] = size;
                            models.Add(modelDict);
                        }
                    }
                }
            }

            Dictionary<string, object> res = new Dictionary<string, object>();
            res["Success"] = true;
            res["Host"] = serverHost;
            res["Path"] = folderPath;
            res["Folders"] = folders;
            res["Models"] = models;
            return res;
        }
    }
}
