namespace Infrastructure.Services.Security.Models.Alert;

public class ThresholdSettings
{
    public int WarningThreshold { get; set; }
    public int CriticalThreshold { get; set; }
    public int TimeWindowMinutes { get; set; }
}