using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BimboClub
{
    public class RevitServerClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _activeBaseUrl;
        private readonly List<string> _candidateBaseUrls;
        private readonly string _userName;
        private readonly string _machineName;

        public string Host { get; }
        public string Version { get; }
        public string DiscoveredVersion { get; private set; }

        public RevitServerClient(string host, string version)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be empty", nameof(host));

            Host = host.Replace("http://", "").Replace("https://", "").Trim().Trim('/');
            Version = version.Trim();
            DiscoveredVersion = Version;

            _candidateBaseUrls = GenerateCandidateUrls(Host, Version);

            _userName = SanitizeHeader(Environment.UserName, "BCCUser");
            _machineName = SanitizeHeader(Environment.MachineName, "BCCMachine");

            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                PreAuthenticate = true,
                UseProxy = true,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(25)
            };
        }

        private static List<string> GenerateCandidateUrls(string host, string preferredVersion)
        {
            var list = new List<string>();

            var orderedYears = new List<string> { preferredVersion, "2022", "2020", "2021", "2024", "2023", "2019", "2018", "2025", "2026" }.Distinct();

            foreach (var ver in orderedYears)
            {
                list.Add($"http://{host}/RevitServerAdminRESTService{ver}/AdminRESTService.svc");
                list.Add($"http://{host}/RevitServerModelDataRESTService{ver}/ModelDataRESTService.svc");
                list.Add($"http://{host}/RevitServerAdminRESTService{ver}/AdminRestService.svc");
                list.Add($"http://{host}/RevitServerAdminRESTService{ver}/AdminService.svc");
                list.Add($"http://{host}/RevitServerRESTService{ver}/RESTService.svc");
                list.Add($"http://{host}/RevitServerRESTService{ver}/AdminRESTService.svc");
                list.Add($"http://{host}/revitserveradminrestservice{ver}/adminrestservice.svc");
            }

            // Unversioned variants
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminRESTService.svc");
            list.Add($"http://{host}/RevitServerModelDataRESTService/ModelDataRESTService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminRestService.svc");
            list.Add($"http://{host}/RevitServerRESTService/RESTService.svc");

            return list.Distinct().ToList();
        }

        private static string SanitizeHeader(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string ascii = new string(value.Where(c => c >= 32 && c <= 126).ToArray());
            return string.IsNullOrWhiteSpace(ascii) ? fallback : ascii;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url, string clientVersion = null)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.TryAddWithoutValidation("User-Name", _userName);
            req.Headers.TryAddWithoutValidation("User-Machine-Name", _machineName);
            req.Headers.TryAddWithoutValidation("Operation-GUID", Guid.NewGuid().ToString());

            string verToSend = clientVersion ?? DiscoveredVersion ?? Version;
            if (!string.IsNullOrEmpty(verToSend))
            {
                req.Headers.TryAddWithoutValidation("Client-Version", verToSend);
            }
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            return req;
        }

        private static string ExtractVersionFromUrl(string url)
        {
            string[] years = { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "2019", "2018" };
            foreach (var y in years)
            {
                if (url.Contains(y)) return y;
            }
            return null;
        }

        private async Task EnsureActiveBaseUrlAsync()
        {
            if (_activeBaseUrl != null) return;

            var candidates = _candidateBaseUrls.ToList();
            var tasks = candidates.Select(async baseUrl =>
            {
                string urlVersion = ExtractVersionFromUrl(baseUrl) ?? Version;
                string[] testEndpoints = { "serverProperties", "%7C/contents", "|/contents" };

                foreach (var ep in testEndpoints)
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                        using (var req = CreateRequest(HttpMethod.Get, $"{baseUrl}/{ep}", urlVersion))
                        {
                            using (var resp = await _httpClient.SendAsync(req, cts.Token))
                            {
                                if (resp.IsSuccessStatusCode)
                                {
                                    return (BaseUrl: baseUrl, Version: urlVersion);
                                }
                            }
                        }
                    }
                    catch { }
                }
                return (BaseUrl: (string)null, Version: (string)null);
            }).ToList();

            var results = await Task.WhenAll(tasks);
            var match = results.FirstOrDefault(r => r.BaseUrl != null);
            if (match.BaseUrl != null)
            {
                _activeBaseUrl = match.BaseUrl;
                DiscoveredVersion = match.Version ?? DiscoveredVersion;
                return;
            }

            _activeBaseUrl = _candidateBaseUrls[0];
        }

        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            await EnsureActiveBaseUrlAsync();

            Exception lastEx = null;

            var urlsToTry = new List<string>();
            if (_activeBaseUrl != null) urlsToTry.Add(_activeBaseUrl);
            foreach (var b in _candidateBaseUrls)
            {
                if (!urlsToTry.Contains(b)) urlsToTry.Add(b);
            }

            foreach (var baseUrl in urlsToTry)
            {
                string url = $"{baseUrl}/{relativeUrl.TrimStart('/')}";
                string urlVersion = ExtractVersionFromUrl(baseUrl) ?? DiscoveredVersion ?? Version;

                try
                {
                    using (var request = CreateRequest(HttpMethod.Get, url, urlVersion))
                    {
                        using (var response = await _httpClient.SendAsync(request))
                        {
                            response.EnsureSuccessStatusCode();

                            string json = await response.Content.ReadAsStringAsync();

                            using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                            {
                                var serializer = new DataContractJsonSerializer(typeof(T));
                                var result = (T)serializer.ReadObject(ms);

                                _activeBaseUrl = baseUrl;
                                DiscoveredVersion = urlVersion;
                                return result;
                            }
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastEx = ex;
                    continue;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    continue;
                }
            }

            throw lastEx ?? new InvalidOperationException($"Не удалось выполнить запрос к Revit Server {Host} ({relativeUrl})");
        }

        public async Task<ServerProperties> CheckConnectionAsync()
        {
            // 1. Try Revit internal proxy first (when running in Revit)
            var proxyResult = QueryViaRevitProxy("|");
            if (proxyResult != null)
            {
                return new ServerProperties
                {
                    ServerName = Host,
                    ServerVersion = DiscoveredVersion ?? Version
                };
            }

            await EnsureActiveBaseUrlAsync();

            try
            {
                var props = await GetAsync<ServerProperties>("serverProperties");
                if (!string.IsNullOrEmpty(props.ServerVersion))
                {
                    DiscoveredVersion = props.ServerVersion;
                }
                return props;
            }
            catch
            {
                var rootContents = await GetContentsAsync("|");
                if (rootContents != null)
                {
                    return new ServerProperties
                    {
                        ServerName = Host,
                        ServerVersion = DiscoveredVersion
                    };
                }

                throw;
            }
        }

        public async Task<FolderContents> GetContentsAsync(string serverRelativePath)
        {
            // 1. Try Revit internal proxy first (when running in Revit)
            var proxyResult = QueryViaRevitProxy(serverRelativePath);
            if (proxyResult != null)
            {
                return proxyResult;
            }

            // 2. Fallback to REST
            string path = string.IsNullOrWhiteSpace(serverRelativePath) || serverRelativePath.Trim() == "|"
                ? "%7C"
                : string.Join("%7C", serverRelativePath.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(p => Uri.EscapeDataString(p.Trim())));

            try
            {
                return await GetAsync<FolderContents>($"{path}/contents");
            }
            catch
            {
                if (path == "%7C")
                {
                    try
                    {
                        return await GetAsync<FolderContents>("|/contents");
                    }
                    catch
                    {
                        return await GetAsync<FolderContents>("root/contents");
                    }
                }
                throw;
            }
        }

        private FolderContents QueryViaRevitProxy(string serverRelativePath)
        {
            try
            {
                Type proxyProviderType = Type.GetType("Autodesk.RevitServer.Enterprise.Common.ClientServer.Proxy.ProxyProvider, RS.Enterprise.Common.ClientServer.Proxy");
                if (proxyProviderType == null) return null;

                System.Reflection.PropertyInfo propInstance = proxyProviderType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (propInstance == null) return null;
                object proxyProvider = propInstance.GetValue(null, null);
                if (proxyProvider == null) return null;

                Type modelServiceType = Type.GetType("Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model.IModelService, RS.Enterprise.Common.ClientServer.ServiceContract.Model");
                if (modelServiceType == null) return null;

                System.Reflection.MethodInfo getBufferedProxyMethod = null;
                foreach (System.Reflection.MethodInfo m in proxyProviderType.GetMethods())
                {
                    if (m.Name == "GetBufferedProxy" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    {
                        getBufferedProxyMethod = m;
                        break;
                    }
                }
                if (getBufferedProxyMethod == null) return null;

                System.Reflection.MethodInfo genericMethod = getBufferedProxyMethod.MakeGenericMethod(modelServiceType);
                object proxy = genericMethod.Invoke(proxyProvider, new object[] { Host });
                if (proxy == null) return null;

                System.Reflection.PropertyInfo propService = proxy.GetType().GetProperty("Service");
                object service = propService != null ? propService.GetValue(proxy, null) : null;
                if (service == null) return null;

                string folderPath = string.IsNullOrWhiteSpace(serverRelativePath) ? "|" : serverRelativePath;

                object listResult = null;
                try
                {
                    System.Reflection.MethodInfo methodList = modelServiceType.GetMethod("ListSubFoldersAndModels", new Type[] { typeof(string) });
                    if (methodList != null) listResult = methodList.Invoke(service, new object[] { folderPath });
                }
                catch { }

                if (listResult == null)
                {
                    try
                    {
                        System.Reflection.MethodInfo methodGetList = modelServiceType.GetMethod("GetListOfModelFilesAndFolders", new Type[] { typeof(string) });
                        if (methodGetList != null) listResult = methodGetList.Invoke(service, new object[] { folderPath });
                    }
                    catch { }
                }

                if (listResult == null) return null;

                var result = new FolderContents
                {
                    Folders = new List<ServerFolder>(),
                    Models = new List<ServerModel>()
                };

                System.Reflection.PropertyInfo propFolders = listResult.GetType().GetProperty("Folders");
                if (propFolders != null)
                {
                    var fList = propFolders.GetValue(listResult, null) as System.Collections.IEnumerable;
                    if (fList != null)
                    {
                        foreach (object f in fList)
                        {
                            if (f != null)
                            {
                                string fName = f.ToString();
                                if (!string.IsNullOrWhiteSpace(fName))
                                {
                                    result.Folders.Add(new ServerFolder { Name = fName, FolderCount = 1 });
                                }
                            }
                        }
                    }
                }

                System.Reflection.PropertyInfo propModels = listResult.GetType().GetProperty("Models");
                if (propModels != null)
                {
                    var mList = propModels.GetValue(listResult, null) as System.Collections.IEnumerable;
                    if (mList != null)
                    {
                        foreach (object m in mList)
                        {
                            if (m == null) continue;
                            System.Reflection.PropertyInfo pName = m.GetType().GetProperty("ModelName") ?? m.GetType().GetProperty("Name");
                            string name = pName != null ? (pName.GetValue(m, null) ?? "").ToString() : m.ToString();

                            long size = 0;
                            System.Reflection.PropertyInfo pSize = m.GetType().GetProperty("ModelSize") ?? m.GetType().GetProperty("Size");
                            if (pSize != null)
                            {
                                object sVal = pSize.GetValue(m, null);
                                if (sVal != null) long.TryParse(sVal.ToString(), out size);
                            }

                            result.Models.Add(new ServerModel { Name = name, Size = size });
                        }
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
