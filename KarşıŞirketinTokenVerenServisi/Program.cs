using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Urls.Add("http://localhost:5200");


app.UseMiddleware<FirewallMiddleware>();

app.MapGet("/token-al", (IConfiguration config) =>
{
    var email = config["GizliKasa:JiraEmail"];
    var token = config["GizliKasa:JiraToken"];
    var projectKey = config["GizliKasa:ProjectKey"];
    var masterKey = config["GizliKasa:EncryptionKey"]; 

    var gercekBilgiler = new
    {
        JiraEmail = email,
        JiraToken = token,
        ProjectKey = projectKey
    };

    string jsonHali = JsonSerializer.Serialize(gercekBilgiler);
    
    // Veriyi AES-256 ile şifreler
    string sifreliData = SecurityHelper.Sifrele(jsonHali, masterKey);

    Console.WriteLine("[SUCCESS] Güvenli istek başarıyla yanıtlandı.");
    return Results.Ok(new { EncryptedPayload = sifreliData });
});

app.Run();