using Application.Enums;

namespace Infrastructure.Services.Monitoring.Models;

public class MetricAlert
{
    public string? Name { get; set; }
    public AlertType Type { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string? Threshold { get; set; }
    public string? Severity { get; set; }
}