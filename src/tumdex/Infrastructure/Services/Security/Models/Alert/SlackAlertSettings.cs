namespace Infrastructure.Services.Security.Models.Alert;

public class SlackAlertSettings
{
    public bool Enabled { get; set; }
    public string Channel { get; set; }
    public string WebhookUrl { get; set; }
}