using Application.Enums;
using Application.Models.Monitoring;

namespace Application.Abstraction.Services;

public interface IAlertService
{
    /// <summary>
    /// Belirtilen türde ve içerikte bir alert gönderir
    /// </summary>
    /// <param name="type">Alert türü (RateLimit, DDoS, HighLatency, vs.)</param>
    /// <param name="message">Alert mesajı</param>
    /// <param name="metadata">Alert ile ilgili ek bilgiler</param>
    Task SendAlertAsync(AlertType type, string message, Dictionary<string, string> metadata);

    /// <summary>
    /// Metrik tabanlı bir alert işler
    /// </summary>
    /// <param name="alert">Alert detayları</param>
    Task ProcessMetricAlert(MetricAlert alert);
}