using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Persistence.DbConfiguration;

public static class DatabaseSettings
{
    public static IServiceCollection AddDatabaseConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DatabaseConfiguration");

        var connectionString = DatabaseConfiguration.GetConnectionString(configuration, logger);

        services.AddDbContext<TumdexDbContext>(options =>
        {
            ConfigureDatabase(options, connectionString, isDevelopment);
            
            logger.LogInformation(
                "Veritabanı yapılandırması tamamlandı. Environment: {Environment}", 
                isDevelopment ? "Development" : "Production");
        });

        return services;
    }

    internal static void ConfigureDatabase(
        DbContextOptionsBuilder options, 
        string connectionString,
        bool isDevelopment = false)
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(TumdexDbContext).Assembly.GetName().Name);
            
            // Transaction yönetimi için
            npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        
            // TransactionScope desteği için
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
            npgsqlOptions.MinBatchSize(1);
        });

        if (isDevelopment)
        {
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
        }
    }
}