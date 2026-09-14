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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
