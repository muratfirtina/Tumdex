namespace Infrastructure.Services.Security.Models.Alert;

public class AlertSettings
{
    public EmailAlertSettings Email { get; set; }
    public SlackAlertSettings Slack { get; set; }
    public Dictionary<string, ThresholdSettings> Thresholds { get; set; }
}