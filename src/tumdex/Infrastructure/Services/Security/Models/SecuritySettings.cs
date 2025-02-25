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
    public int RequestsPerHour { get; set; } = 1000;
    public int MaxConcurrentRequests { get; set; } = 100;
    public int WindowSizeInMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
    public int PermitLimit { get; set; } = 100;
}

public class DDoSConfig
{
    public int MaxConcurrentRequests { get; set; } = 100;
    public int RequestsPerMinuteThreshold { get; set; } = 100;
    public int WindowSizeInSeconds { get; set; } = 60;
    public bool Enabled { get; set; } = true;
}