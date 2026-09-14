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

        public RevitServerRestClient(string host, string version)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be empty", nameof(host));

            Host = host.Replace("http://", "").Replace("https://", "").Trim().Trim('/');
            Version = version.Trim();

            _candidateBaseUrls = new List<string>
            {
                $"http://{Host}/RevitServerAdminRESTService{Version}/AdminRESTService.svc",
                $"http://{Host}/RevitServerAdminRESTService{Version}/AdminRestService.svc",
                $"http://{Host}/RevitServerAdminRESTService/AdminRESTService.svc",
                $"http://{Host}/RevitServerAdminRESTService/AdminRestService.svc"
            };

            _activeBaseUrl = _candidateBaseUrls[0];

            _userName = SanitizeHeader(Environment.UserName, "BCCUser");
            _machineName = SanitizeHeader(Environment.MachineName, "BCCMachine");

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        private static string SanitizeHeader(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            // Clean non-ASCII characters that can break IIS HTTP header parsing
            string ascii = new string(value.Where(c => c >= 32 && c <= 126).ToArray());
            return string.IsNullOrWhiteSpace(ascii) ? fallback : ascii;
        }

        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            Exception? lastEx = null;

            // Try active base URL first, then fallback to other candidates if 404/405 occurs
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
                    return result;
                }
                catch (HttpRequestException ex)
                {
                    lastEx = ex;
                    // If method not allowed or not found on base url, try next candidate
                    continue;
                }
            }

            throw lastEx ?? new InvalidOperationException($"Не удалось выполнить запрос к Revit Server {Host}");
        }

        public async Task<ServerProperties> CheckConnectionAsync()
        {
            // 1. Try serverProperties
            try
            {
                return await GetAsync<ServerProperties>("serverProperties");
            }
            catch
            {
                // 2. Try serverproperties (lowercase)
                try
                {
                    return await GetAsync<ServerProperties>("serverproperties");
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
                            ServerVersion = Version
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
