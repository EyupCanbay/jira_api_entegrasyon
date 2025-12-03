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

        private async Task<CozulmusBilgiler> GizliBilgileriGetirAsync()
        {
            string masterKey = _configuration["GizliKasa:EncryptionKey"];
            if (string.IsNullOrEmpty(masterKey)) throw new Exception("EncryptionKey bulunamadı!");

            using var karsiSirketClient = new HttpClient();
            
            // GÜVENLİK PROTOKOLÜ 
            var timestamp = DateTime.UtcNow.Ticks.ToString(); // Şu anki zaman
            var nonce = Guid.NewGuid().ToString();            // Tek seferlik rastgele kod
            var signature = SecurityHelper.HMACImzaOlustur(timestamp, nonce, masterKey); // İmza

            // Header'lara ekle
            karsiSirketClient.DefaultRequestHeaders.Add("X-Timestamp", timestamp);
            karsiSirketClient.DefaultRequestHeaders.Add("X-Nonce", nonce);
            karsiSirketClient.DefaultRequestHeaders.Add("X-Signature", signature);

            var response = await karsiSirketClient.GetAsync("http://localhost:5200/token-al");
            
            if (!response.IsSuccessStatusCode) 
            {
                var hata = await response.Content.ReadAsStringAsync();
                throw new Exception($"GÜVENLİK HATASI: {response.StatusCode} - {hata}");
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var gelenPaket = JsonSerializer.Deserialize<KarsiSirketResponse>(jsonString, options);

            string temizJson = SecurityHelper.SifreyiCoz(gelenPaket.EncryptedPayload, masterKey);
            return JsonSerializer.Deserialize<CozulmusBilgiler>(temizJson, options);
        }

        public async Task<string> CreateTaskAsync(TaskRequestDto gelenVeri)
        {
            // Güvenli şekilde kimlik bilgilerini al
            var kimlik = await GizliBilgileriGetirAsync();

            // Jira'ya bağlan
            var authBytes = Encoding.ASCII.GetBytes($"{kimlik.JiraEmail}:{kimlik.JiraToken}");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var jiraPayload = new JiraPayload
            {
                fields = new JiraFields
                {
                    project = new JiraKey { key = kimlik.ProjectKey },
                    summary = $"{gelenVeri.Modul} - {gelenVeri.HataBasligi}",
                    issuetype = new JiraName { name = "Task" },
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
                                        text = $"Öncelik: {gelenVeri.Oncelik}\nDetay: {gelenVeri.Detay}" 
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(jiraPayload);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://eypcnbay.atlassian.net/rest/api/3/issue", httpContent);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();
            else
                throw new Exception($"Jira Hatası: {await response.Content.ReadAsStringAsync()}");
        }
    }
}