using Application.Abstraction.Services;
using Application.Abstraction.Services.HubServices;
using Microsoft.Extensions.DependencyInjection;
using SignalR.HubService;

namespace SignalR;

public static class ServiceRegistration
{
    public static void AddSignalRServices(this IServiceCollection services)
    {
        services.AddTransient<IOrderHubService, OrderHubService>();
        services.AddTransient<IVisitorTrackingHubService, VisitorTrackingHubService>();

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.HandshakeTimeout = TimeSpan.FromSeconds(30);
            // KeepAlive süresini kısalt - böylece daha sık ping mesajları gönderilir ve bağlantı kopması hızlıca anlaşılır
            options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            // Client Timeout süresini artır - bu, client'ın server'dan mesaj alamadığında bağlantıyı koparma süresini belirler
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            // Maksimum mesaj boyutu
            options.MaximumReceiveMessageSize = 64 * 1024; // Daha küçük veri boyutu
            options.StreamBufferCapacity = 10;
        }).AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
            // Null özellikleri gönderme - daha küçük payload
            options.PayloadSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });
    }
}