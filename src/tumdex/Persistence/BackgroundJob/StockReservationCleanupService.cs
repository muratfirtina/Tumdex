using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Persistence.BackgroundJob;

public class StockReservationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockReservationCleanupService> _logger;
    private readonly IConfiguration _configuration;
    private const int DefaultCheckIntervalSeconds = 30;

    public StockReservationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<StockReservationCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var checkIntervalStr = _configuration["StockReservation:CleanupIntervalSeconds"];
        var checkInterval = !string.IsNullOrEmpty(checkIntervalStr) && int.TryParse(checkIntervalStr, out int interval) 
            ? interval 
            : DefaultCheckIntervalSeconds;
    
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting stock reservation cleanup check at: {time}", DateTimeOffset.Now);

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var stockReservationService = scope.ServiceProvider.GetRequiredService<IStockReservationService>();
                        await stockReservationService.ReleaseExpiredReservationsAsync();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up expired stock reservations");
                }

                await Task.Delay(TimeSpan.FromSeconds(checkInterval), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stock reservation cleanup service is shutting down gracefully...");
        }
    }
}