using Application.Abstraction.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<QueuedHostedService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public QueuedHostedService(
        IBackgroundTaskQueue taskQueue,
        ILogger<QueuedHostedService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _taskQueue = taskQueue;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queued Hosted Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    // Her işlem için yeni bir scope oluştur
                    await workItem(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing task work item.");
            }
        }

        _logger.LogInformation("Queued Hosted Service is stopping.");
    }
}