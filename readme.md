# Jira API Entegrasyon Projesi

## 📋 İçindekiler
- [Genel Bakış](#-genel-bakış)
- [Proje Yapısı](#-proje-yapısı)
- [Güvenlik Mimarisi](#-güvenlik-mimarisi)
- [Kurulum ve Yapılandırma](#️-kurulum-ve-yapılandırma)
- [Token Servisi](#-token-servisi)
- [Webhook Entegrasyonu](#-webhook-entegrasyonu)
- [Ngrok Kurulumu](#-ngrok-kurulumu)
- [Jira Webhook Ayarları](#-jira-webhook-ayarları)
- [Test ve Debugging](#-test-ve-debugging)
- [Güvenlik Önlemleri](#️-güvenlik-önlemleri)

---

## 🎯 Genel Bakış

Bu proje, Jira ile güvenli entegrasyon sağlayan **enterprise-grade** bir çözümdür. İki ayrı servisten oluşur:

### 1. **JiraEntegrasyonApi** (Ana Servis - Port: 5100)
- Jira webhook'larını dinler
- Task'ları SQL Server veritabanına kaydeder
- Upsert mantığı ile çalışır (Varsa günceller, yoksa ekler)
- Token servisinden güvenli bir şekilde credentials alır

### 2. **KarşiŞirketinTokenVerenServisi** (Token Servisi - Port: 5200)
- Jira credentials'larını güvenli bir şekilde saklar
- AES-256 şifreleme ile veri döner
- Çok katmanlı güvenlik kontrolü yapar
- HMAC-SHA256 dijital imza doğrulama

### 🔒 Güvenlik Seviyesi: **ENTERPRISE GRADE**

**Özellikler:**
- ✅ Gerçek zamanlı webhook entegrasyonu
- ✅ AES-256 bit şifreleme
- ✅ HMAC-SHA256 dijital imza
- ✅ Replay attack koruması
- ✅ IP whitelist güvenliği
- ✅ Otomatik veritabanı senkronizasyonu

---

## 📁 Proje Yapısı

### JiraEntegrasyonApi (Ana Servis)
```
JiraEntegrasyonApi/
├── Controllers/
│   ├── TaskController.cs          # Genel task işlemleri
│   └── WebhookController.cs       # Jira webhook endpoint (UPSERT mantığı)
├── Core/
│   ├── Dtos/
│   │   ├── CreateTaskDto.cs       # Task oluşturma DTO
│   │   └── WebhookDto.cs          # Webhook veri yapısı
│   ├── Entities/
│   │   └── JiraTaskLog.cs         # Veritabanı entity modeli
│   └── Interfaces/
│       ├── IJiraClient.cs         # Jira servis interface
│       └── ISecurityService.cs    # Güvenlik servis interface
├── Infrastructure/
│   ├── Data/
│   │   └── AppDbContext.cs        # Entity Framework DbContext
│   └── Services/
│       ├── JiraClient.cs          # Jira API istemcisi
│       └── SecurityService.cs     # Şifreleme/İmza servisi
├── Migrations/                    # EF Core migration dosyaları
├── Properties/
│   ├── appsettings.json           # Yapılandırma dosyası
│   └── appsettings.Development.json
├── docker-compose.yaml            # Docker compose (opsiyonel)
└── Program.cs                     # Uygulama başlangıcı
```

### KarşiŞirketinTokenVerenServisi (Token Servisi)
```
KarşiŞirketinTokenVerenServisi/
├── Middleware/
│   └── firewallMiddleware.cs     # 5 katmanlı güvenlik kontrolü
├── SecurityHelper.cs              # AES-256 ve HMAC yardımcıları
├── Program.cs                     # Token endpoint (/token-al)
├── appsettings.json              # Gizli bilgiler (GİT'E EKLENMEMELİ!)
└── jira_api_entegrasyon.sln      # Solution dosyası
```

---

## 🔐 Güvenlik Mimarisi

### Token Servisi - 5 Katmanlı Güvenlik

#### **Katman 1: IP Beyaz Liste (IP Whitelist)**
```csharp
// Sadece localhost'tan gelen istekleri kabul eder
if (remoteIp != "::1" && remoteIp != "127.0.0.1")
{
    context.Response.StatusCode = 403; // Forbidden
    await context.Response.WriteAsync("Forbidden: IP Address not allowed.");
    return;
}
```
**🎯 Amaç:** Sadece güvenilir ağlardan erişim sağlanır.

---

#### **Katman 2: Güvenlik Header Kontrolü**

Her istek şu 3 header'ı içermelidir:

| Header Name    | Açıklama                                   | Örnek Değer                               |
|----------------|-------------------------------------------|-------------------------------------------|
| `X-Timestamp`  | İsteğin zamanı (UTC Ticks)                | `638712345678901234`                      |
| `X-Nonce`      | Tek kullanımlık rastgele sayı            | `a7f3e9d2-4b1c-8e5f-9a2d-3c7b6e4f1a8b`    |
| `X-Signature`  | HMAC-SHA256 dijital imza                  | `kL9mN2pQ5rS8tU1vW4xY7zA3bC6dE9fG2hJ5k=`  |

```csharp
if (!context.Request.Headers.TryGetValue("X-Timestamp", out var timestampVal) ||
    !context.Request.Headers.TryGetValue("X-Nonce", out var nonceVal) ||
    !context.Request.Headers.TryGetValue("X-Signature", out var signatureVal))
{
    context.Response.StatusCode = 401;
    await context.Response.WriteAsync("Unauthorized: Missing Security Headers.");
    return;
}
```

**🎯 Amaç:** Her isteğin kimlik doğrulama bilgilerini taşımasını sağlar.

---

#### **Katman 3: Zaman Aşımı Koruması (Replay Attack Prevention)**
```csharp
var requestTime = new DateTime(ticks, DateTimeKind.Utc);
if (DateTime.UtcNow - requestTime > TimeSpan.FromSeconds(5))
{
    context.Response.StatusCode = 408; // Request Timeout
    await context.Response.WriteAsync("Timeout: Request is too old.");
    return;
}
```

**🎯 Amaç:** 5 saniyeden eski istekleri reddeder, böylece "Replay Attack" saldırılarını engeller.

---

#### **Katman 4: HMAC-SHA256 Dijital İmza Doğrulama**
```csharp
var serverSignature = SecurityHelper.HMACImzaOlustur(timestampVal, nonceVal, masterKey);

if (serverSignature != signatureVal)
{
    Console.WriteLine("[HACK ATTEMPT] Geçersiz İmza!");
    context.Response.StatusCode = 401;
    await context.Response.WriteAsync("Unauthorized: Invalid Signature.");
    return;
}
```

**İmza Oluşturma Algoritması:**
```
message = timestamp + ":" + nonce
signature = HMAC-SHA256(message, secretKey)
base64Signature = Base64Encode(signature)
```

**🎯 Amaç:** İsteğin gönderilirken değiştirilmediğini ve yetkili bir kaynaktan geldiğini doğrular.

---

#### **Katman 5: AES-256 Şifreleme**
```csharp
string sifreliData = SecurityHelper.Sifrele(jsonHali, masterKey);
return Results.Ok(new { EncryptedPayload = sifreliData });
```

**Şifreleme Akışı:**
1. Jira credentials JSON formatına dönüştürülür
2. AES-256 algoritması ile şifrelenir
3. IV (Initialization Vector) otomatik oluşturulur
4. IV + Şifreli veri birleştirilir
5. Base64 formatında client'a döndürülür

**🎯 Amaç:** Ağ üzerinden geçen verinin okunamamasını sağlar.


---

## ⚙️ Kurulum ve Yapılandırma

### 🔧 Gereksinimler

Projeyi çalıştırmadan önce aşağıdaki araçların bilgisayarınızda kurulu olması gerekir:

- ✅ **.NET 8 SDK** → [İndirmek için tıklayın](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- ✅ **SQL Server** (LocalDB, Express veya Docker)
- ✅ **Ngrok** (Localhost'u internete açmak için) → [İndirmek için tıklayın](https://ngrok.com/download)
- ✅ **Jira Hesabı** (Cloud veya Server sürümü)
- ✅ **Visual Studio Code** veya **Visual Studio 2022**

---

### 🚀 1. Ana Servis Kurulumu (JiraEntegrasyonApi)

#### **Adım 1: Projeyi İndir ve Paketleri Yükle**
```bash
cd JiraEntegrasyonApi
dotnet restore
```

#### **Adım 2: Veritabanı Bağlantısını Yapılandır**

`appsettings.json` dosyasını açın ve aşağıdaki gibi düzenleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=JiraWebhookDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JiraSettings": {
    "BaseUrl": "https://your-company.atlassian.net",
    "TokenServiceUrl": "http://localhost:5200/token-al",
    "MasterEncryptionKey": "12345678901234567890123456789012"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**⚠️ ÖNEMLİ NOTLAR:**
- `MasterEncryptionKey` **tam olarak 32 karakter** olmalıdır!
- Bu key, Token Servisi'ndeki key ile **aynı** olmalıdır!
- `BaseUrl` kısmını kendi Jira adresinizle değiştirin

#### **Adım 3: Veritabanı Tablolarını Oluştur**
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Bu komutlar aşağıdaki tabloyu oluşturacak:

**JiraTickets Tablosu:**
| Sütun         | Tip           | Açıklama                          |
|---------------|---------------|-----------------------------------|
| Id            | int           | Primary Key                       |
| JiraId        | nvarchar(50)  | Jira'nın kendi ID'si              |
| JiraKey       | nvarchar(50)  | Unique Index (örn: PROJ-123)      |
| Summary       | nvarchar(max) | Görev başlığı                     |
| Description   | nvarchar(max) | Görev açıklaması                  |
| Status        | nvarchar(max) | Görev durumu (To Do, Done vb.)    |
| Assignee      | nvarchar(100) | Atanan kişi                       |
| RawPayload    | nvarchar(max) | Gelen tüm JSON verisi             |
| LastUpdated   | datetime2     | Son güncelleme zamanı             |

#### **Adım 4: Servisi Başlat**
```bash
dotnet run --urls "http://localhost:5100"
```

✅ Uygulama başarıyla çalışıyorsa terminalde şunu göreceksiniz:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5100
```

---

### 🔑 2. Token Servisi Kurulumu (KarşiŞirketinTokenVerenServisi)

#### **Adım 1: Jira API Token'ı Al**

1. Jira'ya giriş yap
2. [Atlassian API Tokens](https://id.atlassian.com/manage-profile/security/api-tokens) sayfasına git
3. **Create API token** butonuna tıkla
4. Token'a bir isim ver (örn: "My Integration")
5. Oluşturulan token'ı kopyala (⚠️ Sadece bir kez gösterilir!)

#### **Adım 2: Yapılandırma Dosyasını Düzenle**

`appsettings.json` dosyasını açın:

```json
{
  "GizliKasa": {
    "JiraEmail": "admin@yourcompany.com",
    "JiraToken": "ATATT3xFfGF0abc123xyz789defghijklmnopqrstuvwxyz",
    "ProjectKey": "PROJ",
    "EncryptionKey": "12345678901234567890123456789012"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**🔐 GÜVENLİK UYARISI:**
Bu dosya **GİZLİ BİLGİLER** içerir! Versiyon kontrolüne eklenmemelidir.

**`.gitignore` dosyanıza ekleyin:**
```gitignore
appsettings.json
appsettings.*.json
*.user
*.suo
bin/
obj/
```

#### **Adım 3: Servisi Başlat**
```bash
cd KarşiŞirketinTokenVerenServisi
dotnet run --urls "http://localhost:5200"
```

✅ Token servisi hazır! Test için:
```bash
curl http://localhost:5200/token-al 
```

---

## 🔑 Token Servisi Kullanımı

### Client Tarafından İstek Yapma

**⚠️ ÖNEMLİ:** Bu kod örneği **projenizde YOK**. Token servisini kullanmak için bu kodu kendiniz eklemeniz gerekir.

**Nereye Eklenmeli?** 
- `Infrastructure/Services/` klasörüne `TokenClient.cs` olarak

**Nasıl Kullanılır?**
```csharp
// Program.cs veya Startup'ta DI'a ekle:
builder.Services.AddHttpClient();

// Controller'da kullan:
public class TaskController : ControllerBase
{
    private readonly ITokenClient _tokenClient;
    
    public TaskController(ITokenClient tokenClient)
    {
        _tokenClient = tokenClient;
    }
    
    [HttpGet("test-token")]
    public async Task TestToken()
    {
        var credentials = await _tokenClient.GetJiraCredentials();
        return Ok(new { email = credentials.JiraEmail });
    }
}
```

**Token Client Kodu (Eklenecek):**

```csharp
// Infrastructure/Services/TokenClient.cs
using System.Text.Json;

public interface ITokenClient
{
    Task GetJiraCredentials();
}

public class TokenClient : ITokenClient
{
    private readonly HttpClient _httpClient;
    private readonly string _tokenServiceUrl;
    private readonly string _masterKey;

    public TokenClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _tokenServiceUrl = config["JiraSettings:TokenServiceUrl"];
        _masterKey = config["JiraSettings:MasterEncryptionKey"];
    }

    public async Task GetJiraCredentials()
    {
        // 1. Güvenlik parametrelerini oluştur
        var timestamp = DateTime.UtcNow.Ticks.ToString();
        var nonce = Guid.NewGuid().ToString();
        var signature = SecurityHelper.HMACImzaOlustur(timestamp, nonce, _masterKey);
        
        // 2. Security Header'larını ekle
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Timestamp", timestamp);
        _httpClient.DefaultRequestHeaders.Add("X-Nonce", nonce);
        _httpClient.DefaultRequestHeaders.Add("X-Signature", signature);
        
        // 3. Token servisine istek at
        var response = await _httpClient.GetAsync(_tokenServiceUrl);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Token alınamadı: {response.StatusCode}");
        }
        
        // 4. Şifreli veriyi al
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<Dictionary>(json);
        string encryptedPayload = result["EncryptedPayload"];
        
        // 5. AES-256 ile şifreyi çöz
        string decryptedJson = SecurityHelper.SifreyiCoz(encryptedPayload, _masterKey);
        
        // 6. JSON'ı objeye dönüştür
        var credentials = JsonSerializer.Deserialize(decryptedJson);
        
        return credentials;
    }
}

public class JiraCredentials
{
    public string JiraEmail { get; set; }
    public string JiraToken { get; set; }
    public string ProjectKey { get; set; }
}
```

**⚠️ SecurityHelper'ı Token Servisinden Ana Servise Kopyalayın:**

Token servisindeki `SecurityHelper.cs` dosyasını ana servise de eklemeniz gerekir:
```
JiraEntegrasyonApi/
└── Infrastructure/
    └── Helpers/
        └── SecurityHelper.cs  ← Token servisinden kopyala
```

### İstek-Yanıt Akışı

**⚠️ ÖNEMLİ:** Bu projede token servisine istek yapmak için **güvenlik header'larını manuel olarak oluşturmanız** gerekir. Proje şu an bunu otomatik yapmıyor.

```
┌──────────────┐                                  ┌───────────────────┐
│  Ana Servis  │                                  │  Token Servisi    │
│ (Port 5100)  │                                  │  (Port 5200)      │
└──────┬───────┘                                  └─────────┬─────────┘
       │                                                    │
       │                                                    │
       │ GET /token-al                                      │
       ├───────────────────────────────────────────────────>│
       │                                                    │
       │                                                    │
       │    { EncryptedPayload: "kL9mN2pQ..." }             │
       │<───────────────────────────────────────────────────┤
       │                                                    │
       │   MANUEL: Şifreyi çöz                             │
       │    SecurityHelper.SifreyiCoz(encrypted, masterKey) │
       │                                                    │
       ▼                                                    │
  ✅ Jira Email, Token, ProjectKey elde edildi              │
```

---

## 🎣 Webhook Entegrasyonu

### WebhookController.cs - UPSERT Mantığı

Ana servisteki webhook controller'ı Jira'dan gelen olayları işler:

```csharp
[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public WebhookController(AppDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task ReceiveJiraHook([FromBody] JiraPayload payload)
    {
        // 1. Basit Validasyon
        if (payload?.Issue == null)
        {
            _logger.LogWarning("Jira'dan boş veya geçersiz istek geldi.");
            return Ok("Ignored");
        }

        try
        {
            var issue = payload.Issue;
            var fields = issue.Fields;

            // 2. Veritabanında bu Key var mı kontrol et
            var existingTicket = await _context.JiraTickets
                .FirstOrDefaultAsync(t => t.JiraKey == issue.Key);

            // JSON serileştirme (Ham veriyi saklamak için)
            string rawJson = JsonConvert.SerializeObject(payload);

            if (existingTicket != null)
            {
                // --- GÜNCELLEME (UPDATE) ---
                _logger.LogInformation($"✏️ Ticket Güncelleniyor: {issue.Key}");
                
                existingTicket.Summary = fields.Summary;
                existingTicket.Description = fields.Description;
                existingTicket.Status = fields.Status?.Name;
                existingTicket.Assignee = fields.Assignee?.DisplayName;
                existingTicket.RawPayload = rawJson;
                existingTicket.LastUpdated = DateTime.UtcNow;
                
                _context.JiraTickets.Update(existingTicket);
            }
            else
            {
                // --- YENİ KAYIT (INSERT) ---
                _logger.LogInformation($"➕ Yeni Ticket Oluşturuluyor: {issue.Key}");

                var newTicket = new JiraTicket
                {
                    JiraId = issue.Id,
                    JiraKey = issue.Key,
                    Summary = fields.Summary,
                    Description = fields.Description,
                    Status = fields.Status?.Name,
                    Assignee = fields.Assignee?.DisplayName,
                    RawPayload = rawJson,
                    LastUpdated = DateTime.UtcNow
                };

                await _context.JiraTickets.AddAsync(newTicket);
            }

            // 3. Değişiklikleri Kaydet
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = "Sync Successful", 
                key = issue.Key,
                action = existingTicket != null ? "Updated" : "Created"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Veritabanı hatası: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
    }
}
```

### Webhook Veri Yapısı (DTO)

```csharp
public class JiraPayload
{
    [JsonProperty("webhookEvent")]
    public string WebhookEvent { get; set; } // "issue_created", "issue_updated"
    
    [JsonProperty("issue")]
    public JiraIssue Issue { get; set; }
}

public class JiraIssue
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("key")]
    public string Key { get; set; }

    [JsonProperty("fields")]
    public JiraFields Fields { get; set; }
}

public class JiraFields
{
    [JsonProperty("summary")]
    public string Summary { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("status")]
    public JiraStatus Status { get; set; }

    [JsonProperty("assignee")]
    public JiraUser Assignee { get; set; }
}
```

---

## 🌍 Ngrok Kurulumu ve Kullanımı

Jira bulutta (cloud) çalışır, sizin servisiniz ise localhost'ta. Jira'nın localhost'unuza erişebilmesi için **tünel** açmanız gerekir.

### **Adım 1: Ngrok İndirme**

1. [Ngrok İndirme Sayfası](https://ngrok.com/download)'na git
2. İşletim sisteminize uygun sürümü indir
3. ZIP'ten çıkar

### **Adım 2: Ngrok Başlatma**

Terminal açın ve projenizin çalıştığı portu belirtin:

```bash
# Windows
ngrok.exe http 5100

# Mac/Linux
./ngrok http 5100
```

### **Adım 3: Public URL'i Kopyala**

Terminal ekranında şöyle bir çıktı göreceksiniz:

```
ngrok

Session Status                online
Account                       [Your Account]
Version                       3.x.x
Region                        Europe (eu)
Latency                       -
Web Interface                 http://127.0.0.1:4040
Forwarding                    https://a1b2-88-99-100.ngrok-free.app -> http://localhost:5100

Connections                   ttl     opn     rt1     rt5     p50     p90
                              0       0       0.00    0.00    0.00    0.00
```

**🔗 Public URL:** `https://a1b2-88-99-100.ngrok-free.app`

Bu URL'i kopyalayın! Jira webhook ayarlarında kullanacaksınız.

### **⚠️ Önemli Notlar:**

- Ngrok'un **ücretsiz** sürümünde, her yeniden başlatmada URL değişir
- URL değişirse Jira webhook ayarlarını güncellemeniz gerekir
- Ücretli sürümde sabit URL alabilirsiniz

---

## 🔗 Jira Webhook Ayarları

### **Adım 1: Jira Yönetim Paneline Git**

1. Jira'ya giriş yapın
2. Sağ üstteki **⚙️ Ayarlar** (Settings) ikonuna tıklayın
3. **System** menüsünü seçin

### **Adım 2: Webhooks Menüsüne Eriş**

Sol menüde aşağı kaydırın ve **Webhooks** seçeneğini bulun.

### **Adım 3: Yeni Webhook Oluştur**

**+ Create a Webhook** butonuna tıklayın.

### **Adım 4: Webhook Formunu Doldur**

| Alan         | Değer                                                                 |
|--------------|-----------------------------------------------------------------------|
| **Name**     | `My Local Integration` (İstediğiniz bir isim)                        |
| **Status**   | ✅ `Enabled`                                                          |
| **URL**      | `https://your-ngrok-url.ngrok-free.app/api/webhook`                  |
| **Description** | `Real-time sync to local database`                                 |

**Events (Hangi Olayları Dinleyeceğiz):**

Issue bölümünden şu kutucukları işaretleyin:

- ☑️ **created** → Yeni task oluşturulduğunda
- ☑️ **updated** → Task güncellendiğinde
- ☑️ **deleted** → Task silindiğinde (opsiyonel)

### **Adım 5: Kaydet ve Test Et**

**Create** butonuna basın.

**Test:**
1. Jira'da yeni bir task oluşturun
2. Ana servisinizin terminalinde şu logu görmelisiniz:
   ```
   info: WebhookController[0] ➕ Yeni Ticket Oluşturuluyor: PROJ-105
   ```

3. Veritabanını kontrol edin:
   ```sql
   SELECT * FROM JiraTickets ORDER BY LastUpdated DESC;
   ```

---
