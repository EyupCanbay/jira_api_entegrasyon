using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraEntegrasyonApi.Models;

namespace JiraEntegrasyonApi.Services
{
    public class JiraService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public JiraService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> CreateTaskAsync(TaskRequestDto gelenVeri)
        {
            var settings = _configuration.GetSection("JiraSettings");
            var baseUrl = settings["BaseUrl"];
            var email = settings["Email"];
            var apiToken = settings["ApiToken"];
            var projectKey = settings["ProjectKey"];

            // 1. Basic Auth Header Oluşturma
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

            // 2. Senin Verini Jira Formatına Çevirme (Mapping)
            var jiraPayload = new JiraPayload
            {
                fields = new JiraFields
                {
                    project = new JiraKey { key = projectKey },
                    summary = $"{gelenVeri.Modul} - {gelenVeri.HataBasligi}",
                    issuetype = new JiraName { name = "Task" }, // Veya "Bug"
                    description = new JiraDescription
                    {
                        content = new List<JiraContent>
                        {
                            new JiraContent
                            {
                                content = new List<JiraTextContent>
                                {
                                    new JiraTextContent 
                                    { 
                                        text = $"Modül: {gelenVeri.Modul}\nÖncelik: {gelenVeri.Oncelik}\n\nDetay:\n{gelenVeri.Detay}" 
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // 3. Gönderim
            var jsonContent = JsonSerializer.Serialize(jiraPayload);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/rest/api/3/issue", httpContent);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(); // Başarılı JSON döner (Issue Key, ID vs.)
            }
            else
            {
                // Hata durumunda Jira'nın verdiği hata mesajını fırlat
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Jira Hatası: {errorMsg}");
            }
        }
    }
}