namespace Infrastructure.Services.Security.Models;

public class SecuritySettings
{
    public JwtSettings JwtSettings { get; set; } = new();
    public TokenSettings TokenSettings { get; set; } = new();
    public RateLimitConfig RateLimitConfig { get; set; } = new();
    public DDoSConfig DDoSConfig { get; set; } = new();
}

public class RateLimitConfig
{
    public int RequestsPerHour { get; set; } = 20000;
    public int AuthenticatedRequestsPerHour { get; set; } = 40000; // Belirtilmezse RequestsPerHour kullanılır
    public int AnonymousRequestsPerHour { get; set; } = 40000; // Belirtilmezse RequestsPerHour/2 kullanılır
    public int WhitelistedRequestsPerMinute { get; set; } = 1000; // Beyaz listedeki endpoint'ler için dakikalık limit
    public int MaxConcurrentRequests { get; set; } = 1000;
    public int WindowSizeInMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
    public int PermitLimit { get; set; } = 900;
    public int ParallelOperations { get; set; } = 1000; // Redis işlemleri için
    public int MaxRequestsPerIpPerMinute { get; set; } = 300;
    public List<string>? WhitelistedEndpoints { get; set; } // Beyaz listedeki endpoint'ler
}

public class DDoSConfig
{
    public int MaxConcurrentRequests { get; set; } = 300;
    public int RequestsPerMinuteThreshold { get; set; } = 500;
    public int WindowSizeInSeconds { get; set; } = 60;
    public bool Enabled { get; set; } = true;
}