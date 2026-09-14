using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using RevitServerManager.Models;

namespace RevitServerManager.Services
{
    public class RevitServerRestClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _activeBaseUrl;
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
            _activeBaseUrl = _candidateBaseUrls[0];

            _userName = SanitizeHeader(Environment.UserName, "BCCUser");
            _machineName = SanitizeHeader(Environment.MachineName, "BCCMachine");

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        private static List<string> GenerateCandidateUrls(string host, string preferredVersion)
        {
            var list = new List<string>();

            // 1. Primary requested version variants
            list.Add($"http://{host}/RevitServerAdminRESTService{preferredVersion}/AdminRESTService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService{preferredVersion}/AdminRestService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService{preferredVersion}/AdminService.svc");
            list.Add($"http://{host}/RevitServerRESTService{preferredVersion}/RESTService.svc");
            list.Add($"http://{host}/RevitServerRESTService{preferredVersion}/AdminRESTService.svc");

            // 2. Unversioned variants
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminRESTService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminRestService.svc");
            list.Add($"http://{host}/RevitServerAdminRESTService/AdminService.svc");
            list.Add($"http://{host}/RevitServerRESTService/RESTService.svc");

            // 3. Fallback versions (in case host runs a different year version of Revit Server)
            string[] otherVersions = { "2024", "2022", "2023", "2025", "2026", "2021", "2020", "2019" };
            foreach (var ver in otherVersions)
            {
                if (ver == preferredVersion) continue;
                list.Add($"http://{host}/RevitServerAdminRESTService{ver}/AdminRESTService.svc");
                list.Add($"http://{host}/RevitServerAdminRESTService{ver}/AdminRestService.svc");
            }

            // 4. Alternate ports (808, 8080)
            list.Add($"http://{host}:808/RevitServerAdminRESTService{preferredVersion}/AdminRESTService.svc");
            list.Add($"http://{host}:8080/RevitServerAdminRESTService{preferredVersion}/AdminRESTService.svc");

            return list.Distinct().ToList();
        }

        private static string SanitizeHeader(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string ascii = new string(value.Where(c => c >= 32 && c <= 126).ToArray());
            return string.IsNullOrWhiteSpace(ascii) ? fallback : ascii;
        }

        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            Exception? lastEx = null;

            var urlsToTry = new List<string> { _activeBaseUrl };
            foreach (var b in _candidateBaseUrls)
            {
                if (!urlsToTry.Contains(b)) urlsToTry.Add(b);
            }

            foreach (var baseUrl in urlsToTry)
            {
                string url = $"{baseUrl}/{relativeUrl.TrimStart('/')}";

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("User-Name", _userName);
                    request.Headers.TryAddWithoutValidation("User-Machine-Name", _machineName);
                    request.Headers.TryAddWithoutValidation("Operation-GUID", Guid.NewGuid().ToString());

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
                // 2. Try serverproperties (lowercase)
                try
                {
                    var props = await GetAsync<ServerProperties>("serverproperties");
                    if (!string.IsNullOrEmpty(props.ServerVersion))
                    {
                        DiscoveredVersion = props.ServerVersion;
                    }
                    return props;
                }
                catch
                {
                    // 3. Fallback: query root contents directly. If root contents succeeds, the server is online!
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
