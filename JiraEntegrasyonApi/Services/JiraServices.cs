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
            using var karsiSirketClient = new HttpClient(); 
            var response = await karsiSirketClient.GetAsync("http://localhost:5200/token-al");
            
            if (!response.IsSuccessStatusCode) 
                throw new Exception("Karşı şirket servisi (AuthServer) cevap vermiyor! Port 5200 açık mı?");

            var jsonString = await response.Content.ReadAsStringAsync();
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var gelenPaket = JsonSerializer.Deserialize<KarsiSirketResponse>(jsonString, options);

            if (gelenPaket == null || string.IsNullOrEmpty(gelenPaket.EncryptedPayload))
                throw new Exception("Karşı şirketten boş veri geldi!");


            string masterKey = _configuration["GizliKasa:EncryptionKey"];

            if (string.IsNullOrEmpty(masterKey))
                throw new Exception("HATA: appsettings.json dosyasında 'EncryptionKey' bulunamadı!");

            if (masterKey.Length != 32)
                throw new Exception("HATA: EncryptionKey tam 32 karakter olmalıdır!");

            string temizJson = SecurityHelper.SifreyiCoz(gelenPaket.EncryptedPayload, masterKey);

            var cozulmusVeri = JsonSerializer.Deserialize<CozulmusBilgiler>(temizJson, options);
            
            if (cozulmusVeri == null) throw new Exception("Şifre çözüldü ama veri formatı hatalı.");

            return cozulmusVeri;
        }

        public async Task<string> CreateTaskAsync(TaskRequestDto gelenVeri)
        {
            var kimlik = await GizliBilgileriGetirAsync();

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
                                    new JiraTextContent { text = $"Öncelik: {gelenVeri.Oncelik}\nDetay: {gelenVeri.Detay}" }
                                }
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(jiraPayload);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Jira Base URL'si
            var response = await _httpClient.PostAsync("https://eypcnbay.atlassian.net/rest/api/3/issue", httpContent);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();
            else
                throw new Exception($"Jira Hatası: {await response.Content.ReadAsStringAsync()}");
        }
    }
}