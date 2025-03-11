using System;
using System.Threading;
using System.Threading.Tasks;
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
        _logger.LogInformation("Queued Hosted Service başlatılıyor.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                // Her görev için yeni bir scope oluştur
                using var scope = _serviceScopeFactory.CreateScope();
                var serviceProvider = scope.ServiceProvider;

                try
                {
                    await workItem(serviceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Arka plan görevi çalıştırılırken hata oluştu");
                }
            }
            catch (OperationCanceledException)
            {
                // Beklenen iptal durumu
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kuyruk işleme hatası");
            }
        }

        _logger.LogInformation("Queued Hosted Service durduruluyor.");
    }
}