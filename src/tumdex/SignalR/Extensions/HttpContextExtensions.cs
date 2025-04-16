using Microsoft.AspNetCore.Http;

namespace SignalR.Extensions;

public static class HttpContextExtensions
{
    public static string GetRealIpAddress(this HttpContext context)
    {
        string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                    context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "Bilinmiyor";
                    
        // Virgülle ayrılmış birden fazla IP varsa, ilkini al
        if (ip.Contains(","))
        {
            ip = ip.Split(',')[0].Trim();
        }
        
        // IPv6 "::1" yerel adresini yakalamak için
        if (ip == "::1")
        {
            ip = "127.0.0.1";
        }
        
        return ip;
    }
}