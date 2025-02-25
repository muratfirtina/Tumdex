using Core.Persistence.Repositories;

namespace Domain;

public class AlertLog : Entity<string>
{
    public string Type { get; set; }
    public string Message { get; set; }
    public string Metadata { get; set; }
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Threshold { get; set; }
    public string Severity { get; set; }
    public AlertLog() : base("AlertLog")
    {
        Timestamp = DateTime.UtcNow;
    }
}