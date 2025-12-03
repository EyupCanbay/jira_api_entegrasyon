using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Urls.Add("http://localhost:5200");

app.MapGet("/token-al", (IConfiguration config) =>
{
    var email = config["GizliKasa:JiraEmail"];
    var token = config["GizliKasa:JiraToken"];
    var projectKey = config["GizliKasa:ProjectKey"];
    var masterKey = config["GizliKasa:EncryptionKey"]; 

    if (string.IsNullOrEmpty(masterKey)) return Results.Problem("Encryption Key bulunamadı!");

    var gercekBilgiler = new
    {
        JiraEmail = email,
        JiraToken = token,
        ProjectKey = projectKey,
    };

    string jsonHali = JsonSerializer.Serialize(gercekBilgiler);

    string sifreliData = SecurityHelper.Sifrele(jsonHali, masterKey);

    return Results.Ok(new { EncryptedPayload = sifreliData });
});

app.Run();