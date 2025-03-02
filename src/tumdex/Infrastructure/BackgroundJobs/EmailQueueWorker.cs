using Application.Abstraction.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class EmailQueueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailQueueWorker> _logger;
    private readonly TimeSpan _processInterval = TimeSpan.FromMinutes(1);

    public EmailQueueWorker(
        IServiceProvider serviceProvider,
        ILogger<EmailQueueWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Queue Worker is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing queued emails");
            }

            await Task.Delay(_processInterval, stoppingToken);
        }
    }

    private async Task ProcessQueuedEmailsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var emailQueueService = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();

        await emailQueueService.ProcessQueuedEmailsAsync(stoppingToken);
    }
}