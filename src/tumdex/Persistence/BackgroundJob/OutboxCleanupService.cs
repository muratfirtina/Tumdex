using Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Persistence.Context;
using Persistence.Models;

namespace Persistence.BackgroundJob;

public class OutboxCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly OutboxSettings _settings;

    public OutboxCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxCleanupService> logger,
        IOptions<OutboxSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOutboxMessages(stoppingToken);
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up outbox messages");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Outbox cleanup service is shutting down gracefully...");
        }
    }

    private async Task CleanupOutboxMessages(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TumdexDbContext>();

        try
        {
            var successfulCutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(_settings.RetentionDaysSuccess));
            var failedCutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(_settings.RetentionDaysFailed));

            var completedMessages = await context.OutboxMessages
                .Where(m => m.Status == OutboxStatus.Completed && m.ProcessedAt < successfulCutoff)
                .Take(_settings.CleanupBatchSize)
                .ToListAsync(cancellationToken);

            var failedMessages = await context.OutboxMessages
                .Where(m => m.Status == OutboxStatus.Failed && m.ProcessedAt < failedCutoff)
                .Take(_settings.CleanupBatchSize)
                .ToListAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            context.OutboxMessages.RemoveRange(completedMessages);
            context.OutboxMessages.RemoveRange(failedMessages);

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cleaned up {CompletedCount} completed and {FailedCount} failed outbox messages",
                completedMessages.Count,
                failedMessages.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during outbox message cleanup");
            throw;
        }
    }
}