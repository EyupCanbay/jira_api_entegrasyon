using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraEntegrasyonApi.Core.Dtos;
using JiraEntegrasyonApi.Core.Interfaces;

namespace JiraEntegrasyonApi.Infrastructure.Services
{
    public class JiraClient : IJiraClient
    {
        private readonly HttpClient _httpClient;
        private readonly ISecurityService _securityService;

        public JiraClient(HttpClient httpClient, ISecurityService securityService)
        {
            _httpClient = httpClient;
            _securityService = securityService;
        }

        public async Task<string> CreateIssueAsync(CreateTaskDto dto)
        {
            var creds = await _securityService.GetJiraCredentialsAsync();

            var authBytes = Encoding.ASCII.GetBytes($"{creds.Email}:{creds.Token}");
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var payload = new
            {
                fields = new
                {
                    project = new { key = creds.ProjectKey },
                    summary = $"{dto.Modul} - {dto.HataBasligi}",
                    issuetype = new { name = "Task" },
                    description = new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new
                            {
                                type = "paragraph",
                                content = new[]
                                {
                                    new { type = "text", text = $"Öncelik: {dto.Oncelik}\nDetay: {dto.Detay}" }
                                }
                            }
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("https://eypcnbay.atlassian.net/rest/api/3/issue", payload);
            
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Jira API Hatası: {err}");
            }

            // 5. Jira Key'i için
            var resultJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resultJson);
            return doc.RootElement.GetProperty("key").GetString() ?? "UNKNOWN";
        }
    }
}