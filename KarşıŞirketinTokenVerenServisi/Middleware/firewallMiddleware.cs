using System.Net; // HttpStatusCode için

public class FirewallMiddleware
{
    private readonly RequestDelegate _next;

    public FirewallMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        var masterKey = config["GizliKasa:EncryptionKey"];

        // ip kontrolu 
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        // ::1 (IPv6 localhost) ve 127.0.0.1 (IPv4 localhost) izni
        if (remoteIp != "::1" && remoteIp != "127.0.0.1")
        {
            Console.WriteLine($"[SECURITY ALERT] Yetkisiz IP: {remoteIp}");
            context.Response.StatusCode = 403; 
            await context.Response.WriteAsync("Forbidden: IP Address not allowed.");
            return;
        }

        // header kontrolü
        if (!context.Request.Headers.TryGetValue("X-Timestamp", out var timestampVal) ||
            !context.Request.Headers.TryGetValue("X-Nonce", out var nonceVal) ||
            !context.Request.Headers.TryGetValue("X-Signature", out var signatureVal))
        {
            context.Response.StatusCode = 401; 
            await context.Response.WriteAsync("Unauthorized: Missing Security Headers.");
            return;
        }

        // zaman aşımı kontrolu
        if (long.TryParse(timestampVal, out var ticks))
        {
            var requestTime = new DateTime(ticks, DateTimeKind.Utc);
            if (DateTime.UtcNow - requestTime > TimeSpan.FromSeconds(5))
            {
                context.Response.StatusCode = 408; 
                await context.Response.WriteAsync("Timeout: Request is too old (Replay Attack Protection).");
                return;
            }
        }

        // 4. imza oğrulama HMAC
        var serverSignature = SecurityHelper.HMACImzaOlustur(timestampVal, nonceVal, masterKey);
        
        if (serverSignature != signatureVal)
        {
            Console.WriteLine("[HACK ATTEMPT] Geçersiz İmza!");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Invalid Signature.");
            return;
        }

        await _next(context);
    }
}