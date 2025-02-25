using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Persistence.DbConfiguration;

/// <summary>
/// Entity Framework Core migrations için design-time DbContext factory.
/// Migration komutları çalıştırıldığında bu factory kullanılır.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TumdexDbContext>
{
    public TumdexDbContext CreateDbContext(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole()
                .SetMinimumLevel(LogLevel.Information));

        var logger = loggerFactory.CreateLogger<DesignTimeDbContextFactory>();
    
        try
        {
            var connectionString = DatabaseConfiguration.GetConnectionString(
                new ConfigurationBuilder().Build(), 
                logger);

            var optionsBuilder = new DbContextOptionsBuilder<TumdexDbContext>();
            optionsBuilder
                .UseNpgsql(connectionString)
                .EnableSensitiveDataLogging()
                .LogTo(message => logger.LogInformation(message));

            return new TumdexDbContext(optionsBuilder.Options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DbContext oluşturulurken hata oluştu");
            throw;
        }
    }
}