using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JiraEntegrasyonApi.Core.Interfaces;

namespace JiraEntegrasyonApi.Infrastructure.Services
{
    public class RemoteResponse 
    { 
        public string EncryptedPayload { get; set; } = string.Empty; 
    }

    public class SecurityService : ISecurityService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public SecurityService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public async Task<JiraCredentials> GetJiraCredentialsAsync()
        {
            string masterKey = _config["GizliKasa:EncryptionKey"] 
                               ?? throw new Exception("EncryptionKey bulunamadı!");

            var timestamp = DateTime.UtcNow.Ticks.ToString();
            var nonce = Guid.NewGuid().ToString();
            var signature = HmacImzaOlustur(timestamp, nonce, masterKey);

            using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5200/token-al");
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add("X-Nonce", nonce);
            request.Headers.Add("X-Signature", signature);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Token Servisi Hatası: {response.StatusCode}");

            var jsonString = await response.Content.ReadAsStringAsync();
            var remoteData = JsonSerializer.Deserialize<RemoteResponse>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (remoteData == null || string.IsNullOrEmpty(remoteData.EncryptedPayload))
                throw new Exception("Şifreli veri boş geldi.");

            var decryptedJson = SifreyiCoz(remoteData.EncryptedPayload, masterKey);
            
            using var doc = JsonDocument.Parse(decryptedJson);
            var root = doc.RootElement;
            
            return new JiraCredentials(
                root.GetProperty("JiraEmail").GetString() ?? "",
                root.GetProperty("JiraToken").GetString() ?? "",
                root.GetProperty("ProjectKey").GetString() ?? ""
            );
        }

        private string SifreyiCoz(string cipherText, string key)
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            // İlk 16 byte IV'dir
            var iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            
            var cipher = new byte[fullCipher.Length - iv.Length];
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        private string HmacImzaOlustur(string timestamp, string nonce, string secretKey)
        {
            var message = $"{timestamp}:{nonce}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToBase64String(hash);
        }
    }
}