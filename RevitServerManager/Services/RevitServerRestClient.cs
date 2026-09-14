using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using RevitServerManager.Models;

namespace RevitServerManager.Services
{
    public class RevitServerRestClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string? _activeBaseUrl;
        private readonly List<string> _candidateBaseUrls;
        private readonly string _userName;
        private readonly string _machineName;

        public string Host { get; }
        public string Version { get; }
        public string DiscoveredVersion { get; private set; }

        public RevitServerRestClient(string host, string version)
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

            // 1. Primary requested version - Admin & ModelData (Revit Server standard)
            list.Add($"http://{host}/RevitServerAdminRESTService{preferredVersion}/AdminRESTService.svc");
            list.Add($"http://{host}/RevitServerModelDataRESTService{preferredVersion}/ModelDataRESTService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService{preferredVersion}/AdminRestService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService{preferredVersion}/AdminService.svc");
            list.Add($"http://{host}/RevitServerRESTService{preferredVersion}/RESTService.svc");
            list.Add($"http://{host}/RevitServerRESTService{preferredVersion}/AdminRESTService.svc");

            // 2. Unversioned variants
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminRESTService.svc");
            list.Add($"http://{host}/RevitServerModelDataRESTService/ModelDataRESTService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminRestService.svc");
            list.Add($"http://{host}/RevitServerRESTService/RESTService.svc");

            // 3. Fallback versions (2022, 2024, 2023, etc.)
            string[] otherVersions = { "2022", "2024", "2023", "2025", "2026", "2021", "2020", "2019" };
            foreach (var ver in otherVersions)
            {
                if (ver == preferredVersion) continue;
                list.Add($"http://{host}/RevitServerAdminRESTService{ver}/AdminRESTService.svc");
                list.Add($"http://{host}/RevitServerModelDataRESTService{ver}/ModelDataRESTService.svc");
            }

            return list.Distinct().ToList();
        }

        private static string SanitizeHeader(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string ascii = new string(value.Where(c => c >= 32 && c <= 126).ToArray());
            return string.IsNullOrWhiteSpace(ascii) ? fallback : ascii;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.TryAddWithoutValidation("User-Name", _userName);
            req.Headers.TryAddWithoutValidation("User-Machine-Name", _machineName);
            req.Headers.TryAddWithoutValidation("Operation-GUID", Guid.NewGuid().ToString());
            req.Headers.TryAddWithoutValidation("Client-Version", Version);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            return req;
        }

        private async Task EnsureActiveBaseUrlAsync()
        {
            if (_activeBaseUrl != null) return;

            // Probe candidate URLs in parallel to quickly find the responsive endpoint
            var candidates = _candidateBaseUrls.ToList();
            int batchSize = 4;
            for (int i = 0; i < candidates.Count; i += batchSize)
            {
                var batch = candidates.Skip(i).Take(batchSize).ToList();
                var tasks = batch.Select(async baseUrl =>
                {
                    string[] testEndpoints = { "contents", "%7C/contents", "serverProperties", "|/contents" };
                    foreach (var ep in testEndpoints)
                    {
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            using var req = CreateRequest(HttpMethod.Get, $"{baseUrl}/{ep}");

                            using var resp = await _httpClient.SendAsync(req, cts.Token);
                            if (resp.IsSuccessStatusCode)
                            {
                                return baseUrl;
                            }
                        }
                        catch { }
                    }
                    return (string?)null;
                }).ToList();

                var results = await Task.WhenAll(tasks);
                var found = results.FirstOrDefault(r => r != null);
                if (found != null)
                {
                    _activeBaseUrl = found;
                    ExtractDiscoveredVersion(found);
                    return;
                }
            }

            _activeBaseUrl = _candidateBaseUrls[0];
        }

        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            await EnsureActiveBaseUrlAsync();

            Exception? lastEx = null;

            var urlsToTry = new List<string>();
            if (_activeBaseUrl != null) urlsToTry.Add(_activeBaseUrl);
            foreach (var b in _candidateBaseUrls)
            {
                if (!urlsToTry.Contains(b)) urlsToTry.Add(b);
            }

            foreach (var baseUrl in urlsToTry)
            {
                string url = $"{baseUrl}/{relativeUrl.TrimStart('/')}";

                try
                {
                    using var request = CreateRequest(HttpMethod.Get, url);
                    using var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();

                    using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    var result = (T)serializer.ReadObject(ms)!;

                    _activeBaseUrl = baseUrl;
                    ExtractDiscoveredVersion(baseUrl);
                    return result;
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

        private void ExtractDiscoveredVersion(string workingUrl)
        {
            try
            {
                string[] years = { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "2019" };
                foreach (var y in years)
                {
                    if (workingUrl.Contains(y))
                    {
                        DiscoveredVersion = y;
                        break;
                    }
                }
            }
            catch { }
        }

        public async Task<ServerProperties> CheckConnectionAsync()
        {
            await EnsureActiveBaseUrlAsync();

            // 1. Try serverProperties
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
                // 2. Fallback: query root contents directly. If root contents succeeds, the server is online!
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
            string formattedPath;
            if (string.IsNullOrWhiteSpace(serverRelativePath) || serverRelativePath.Trim() == "|")
            {
                formattedPath = "%7C";
            }
            else
            {
                string[] parts = serverRelativePath.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                string[] escapedParts = new string[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    escapedParts[i] = Uri.EscapeDataString(parts[i].Trim());
                }
                formattedPath = string.Join("%7C", escapedParts);
            }

            try
            {
                return await GetAsync<FolderContents>($"{formattedPath}/contents");
            }
            catch (Exception) when (string.IsNullOrWhiteSpace(serverRelativePath) || serverRelativePath.Trim() == "|")
            {
                try
                {
                    return await GetAsync<FolderContents>("|/contents");
                }
                catch
                {
                    return await GetAsync<FolderContents>("contents");
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
