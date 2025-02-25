using Core.Persistence.Repositories;

namespace Domain;

public class SecurityLog : Entity<string>
{
    public DateTime Timestamp { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? ClientIP { get; set; }
    public string? Path { get; set; }
    public string? UserAgent { get; set; }
    public string? UserName { get; set; }
    public string? Exception { get; set; }
    public string? EventType { get; set; }
    public int RequestCount { get; set; }
    public int MaxRequests { get; set; }
    public string? AdditionalInfo { get; set; }
    
    public SecurityLog() : base("SecurityLog")
    {
        Timestamp = DateTime.UtcNow;
    }
}