// Persistence/Services/DatabaseMigrationService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Persistence.Services;

public class DatabaseMigrationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IHostEnvironment _environment;

    public DatabaseMigrationService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseMigrationService> logger,
        IHostEnvironment environment)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Veritabanı migration ve seeding işlemleri başlatılıyor...");
            
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TumdexDbContext>();

            // Önce migration'ları kontrol et ve uygula
            if (context.Database.GetPendingMigrations().Any())
            {
                _logger.LogInformation("Bekleyen migration'lar uygulanıyor...");
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Migration'lar başarıyla uygulandı");
            }
            else
            {
                _logger.LogInformation("Bekleyen migration bulunmuyor");
            }

            // Ardından seed verilerini ekle
            await RoleAndUserSeeder.SeedAsync(scope.ServiceProvider);
            _logger.LogInformation("Seeding işlemleri başarıyla tamamlandı");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanı başlatma işlemi sırasında bir hata oluştu");
            throw; // Kritik bir hata olduğu için uygulamayı başlatmayı durdur
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}