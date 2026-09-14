using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using RevitServerManager.Models;

namespace RevitServerManager.Services
{
    public class RevitServerRestClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _userName;
        private readonly string _machineName;

        public string Host { get; }
        public string Version { get; }

        public RevitServerRestClient(string host, string version)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be empty", nameof(host));

            Host = host.Replace("http://", "").Replace("https://", "").Trim('/');
            Version = version.Trim();

            // Format: http://<host>/RevitServerAdminRESTService<version>/AdminRESTService.svc
            _baseUrl = $"http://{Host}/RevitServerAdminRESTService{Version}/AdminRESTService.svc";
            
            _userName = Environment.UserName;
            _machineName = Environment.MachineName;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            string url = $"{_baseUrl}/{relativeUrl.TrimStart('/')}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Name", _userName);
            request.Headers.Add("User-Machine-Name", _machineName);
            request.Headers.Add("Operation-GUID", Guid.NewGuid().ToString());

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var serializer = new DataContractJsonSerializer(typeof(T));
            return (T)serializer.ReadObject(ms)!;
        }

        public async Task<ServerProperties> CheckConnectionAsync()
        {
            return await GetAsync<ServerProperties>("serverProperties");
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
