using Application.Enums;

namespace Application.Models.Monitoring;

public class AlertConfiguration
{
    /// <summary>
    /// Alert tipi başına maksimum alert sayısı
    /// </summary>
    public Dictionary<AlertType, int> MaxAlertsPerHour { get; set; }

    /// <summary>
    /// Alert seviyesi başına bildirim kanalları
    /// </summary>
    public Dictionary<string, List<string>> NotificationChannels { get; set; }

    /// <summary>
    /// Alert gruplandırma süresi
    /// </summary>
    public TimeSpan GroupingWindow { get; set; }

    /// <summary>
    /// Alert sessizleştirme süresi
    /// </summary>
    public TimeSpan SilenceDuration { get; set; }

    /// <summary>
    /// Auto-resolve süresi
    /// </summary>
    public TimeSpan AutoResolveDuration { get; set; }

    public AlertConfiguration()
    {
        MaxAlertsPerHour = new Dictionary<AlertType, int>
        {
            { AlertType.RateLimit, 100 },
            { AlertType.DDoS, 50 },
            { AlertType.HighLatency, 100 },
            { AlertType.CacheFailure, 50 },
            { AlertType.SecurityThreat, 20 },
            { AlertType.DatabaseError, 50 },
            { AlertType.SystemError, 100 },
            { AlertType.ServiceDown, 20 },
            { AlertType.CriticalError, 20 },
            { AlertType.Warning, 200 },
            { AlertType.Info, 500 }
        };

        NotificationChannels = new Dictionary<string, List<string>>
        {
            { "critical", new List<string> { "email", "slack", "sms" } },
            { "error", new List<string> { "email", "slack" } },
            { "warning", new List<string> { "slack" } },
            { "info", new List<string> { "slack" } }
        };

        GroupingWindow = TimeSpan.FromMinutes(5);
        SilenceDuration = TimeSpan.FromHours(1);
        AutoResolveDuration = TimeSpan.FromHours(24);
    }
}