using System.Text;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Utilities;
using Application.Enums;
using Application.Models.Monitoring;
using Domain;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Persistence.Context;

namespace Infrastructure.Services.Monitoring.Alerts;

public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notificationService;
    private readonly IMetricsService _metricsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ICacheService _cacheService;
    private readonly SemaphoreSlim _alertThrottler;
    private readonly AlertConfiguration _alertConfig;

    public AlertService(
        ILogger<AlertService> logger,
        IConfiguration configuration,
        INotificationService notificationService,
        IMetricsService metricsService,
        IServiceScopeFactory serviceScopeFactory,
        ICacheService cacheService,
        IOptions<AlertConfiguration> alertConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _notificationService = notificationService;
        _metricsService = metricsService;
        _serviceScopeFactory = serviceScopeFactory;
        _cacheService = cacheService;
        _alertConfig = alertConfig.Value;
        _alertThrottler = new SemaphoreSlim(3, 3); // Max 3 concurrent alert processing
    }

    public async Task SendAlertAsync(AlertType type, string message, Dictionary<string, string> metadata)
    {
        try
        {
            metadata = metadata ?? new Dictionary<string, string>();
            // Alert throttling check
            var throttleKey = $"alert_throttle_{type}_{DateTime.UtcNow:yyyyMMddHH}";
            if (await ShouldThrottleAlert(type, throttleKey))
            {
                _logger.LogWarning("Alert throttled: {AlertType}", type);
                return;
            }

            // Concurrency limit check
            if (!await _alertThrottler.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Alert processing throttled due to high concurrency");
                return;
            }

            try
            {
                // Determine severity
                var severity = metadata.GetValueOrDefault("severity", "info");

                // Create alert object
                var alert = new MetricAlert
                {
                    Type = type,
                    Message = message,
                    Labels = metadata,
                    Timestamp = DateTime.UtcNow,
                    Severity = severity,
                    Source = metadata.GetValueOrDefault("source", "system"),
                    Value = metadata.ContainsKey("value") ? double.Parse(metadata["value"]) : 0,
                    Threshold = metadata.GetValueOrDefault("threshold", "N/A")
                };

                await ProcessMetricAlert(alert);
            }
            finally
            {
                _alertThrottler.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert: {AlertType} - {Message}", type, message);
        }
    }

    public async Task ProcessMetricAlert(MetricAlert alert)
    {
        try
        {
            // Check if similar alert is already active
            if (await IsSimilarAlertActive(alert))
            {
                _logger.LogDebug("Similar alert is active, skipping: {AlertType}", alert.Type);
                return;
            }

            // Log alert
            _logger.LogWarning("Alert triggered: {AlertType} - {Message}",
                alert.Type, alert.Message);

            // Save to database
            await SaveAlertToDatabase(alert);

            // Send notifications based on severity
            await SendNotifications(alert);

            // Record metrics
            RecordAlertMetrics(alert);

            // Cache alert for deduplication
            await CacheActiveAlert(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process alert: {AlertType}", alert.Type);
            throw;
        }
    }

    private async Task<bool> ShouldThrottleAlert(AlertType type, string key)
    {
        var count = await _cacheService.GetCounterAsync(key);
        var maxAlerts = _alertConfig.MaxAlertsPerHour.GetValueOrDefault(type, 100);

        if (count >= maxAlerts)
            return true;

        await _cacheService.IncrementAsync(key, 1, TimeSpan.FromHours(1));
        return false;
    }

    private async Task<bool> IsSimilarAlertActive(MetricAlert alert)
    {
        var key = $"active_alert_{alert.Type}_{alert.Source}";
        var existingAlert = await _cacheService.GetOrCreateAsync<MetricAlert>(
            key,
            async () => null,
            _alertConfig.GroupingWindow);

        return existingAlert != null;
    }

    private async Task CacheActiveAlert(MetricAlert alert)
    {
        var key = $"active_alert_{alert.Type}_{alert.Source}";
        await _cacheService.SetManyAsync(
            new Dictionary<string, MetricAlert> { { key, alert } },
            _alertConfig.GroupingWindow);
    }

    private async Task SaveAlertToDatabase(MetricAlert alert)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TumdexDbContext>();
        
        var alertLog = new AlertLog
        {
            Type = alert.Type.ToString(),
            Message = alert.Message,
            Metadata = JsonConvert.SerializeObject(alert.Labels),
            Timestamp = alert.Timestamp,
            Value = alert.Value,
            Threshold = alert.Threshold,
            Severity = alert.Severity
        };

        await dbContext.AlertLogs.AddAsync(alertLog);
        await dbContext.SaveChangesAsync();
    }

    private async Task SendNotifications(MetricAlert alert)
    {
        // Get notification channels for severity
        var channels = _alertConfig.NotificationChannels
            .GetValueOrDefault(alert.Severity.ToLower(), new List<string>());

        var notificationTasks = new List<Task>();

        foreach (var channel in channels)
        {
            switch (channel.ToLower())
            {
                case "email" when _configuration.GetValue<bool>("Monitoring:Alerts:Email:Enabled"):
                    notificationTasks.Add(_notificationService.SendEmailAsync(
                        alert.Type.ToString(),
                        FormatAlertMessage(alert)
                    ));
                    break;

                case "slack" when _configuration.GetValue<bool>("Monitoring:Alerts:Slack:Enabled"):
                    notificationTasks.Add(_notificationService.SendSlackMessageAsync(
                        FormatAlertMessage(alert)
                    ));
                    break;

                case "sms" when _configuration.GetValue<bool>("Monitoring:Alerts:SMS:Enabled"):
                    // SMS implementation
                    break;
            }
        }

        if (notificationTasks.Any())
        {
            await Task.WhenAll(notificationTasks);
        }
    }

    private void RecordAlertMetrics(MetricAlert alert)
    {
        try
        {
            if (alert == null)
                return;

            var labels = new Dictionary<string, string>
            {
                ["severity"] = alert.Severity ?? "unknown",
                ["source"] = alert.Source ?? "system"
            };

            _metricsService.IncrementAlertCounter(
                alert.Type.ToString(),
                labels
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record alert metrics for type: {AlertType}", alert?.Type);
            // Metrik kaydı başarısız olsa bile alert işlemeye devam et
        }
    }

    private string FormatAlertMessage(MetricAlert alert)
    {
        var sb = new StringBuilder();
        
        // Add appropriate emoji based on severity
        var severityEmoji = alert.Severity.ToLower() switch
        {
            "critical" => "🚨",
            "error" => "❌",
            "warning" => "⚠️",
            _ => "ℹ️"
        };

        sb.AppendLine($"{severityEmoji} Alert: {alert.Type}");
        sb.AppendLine($"Severity: {alert.Severity}");
        sb.AppendLine($"Message: {alert.Message}");
        sb.AppendLine($"Time: {alert.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"Source: {alert.Source}");

        if (alert.Labels?.Any() == true)
        {
            sb.AppendLine("\nMetadata:");
            foreach (var label in alert.Labels.Where(l => l.Key != "severity" && l.Key != "source"))
            {
                sb.AppendLine($"- {label.Key}: {label.Value}");
            }
        }

        if (alert.Value != 0)
        {
            sb.AppendLine($"\nValue: {alert.Value}");
            sb.AppendLine($"Threshold: {alert.Threshold}");
        }

        if (!string.IsNullOrEmpty(alert.Details))
        {
            sb.AppendLine($"\nDetails: {alert.Details}");
        }

        return sb.ToString();
    }
}