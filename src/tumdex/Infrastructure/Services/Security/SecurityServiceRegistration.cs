using Application.Abstraction.Services;
using Application.Abstraction.Services.Configurations;
using Application.Services;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Infrastructure.Middleware.DDosProtection;
using Infrastructure.Middleware.Monitoring;
using Infrastructure.Middleware.RateLimiting;
using Infrastructure.Middleware.Security;
using Infrastructure.Services.Cache;
using Infrastructure.Services.Mail;
using Infrastructure.Services.Mail.Models;
using Infrastructure.Services.Monitoring;
using Infrastructure.Services.Monitoring.Alerts;
using Infrastructure.Services.Notifications;
using Infrastructure.Services.Security.Encryption;
using Infrastructure.Services.Security.JWT;
using Infrastructure.Services.Security.KeyVault;
using Infrastructure.Services.Security.Models;
using Infrastructure.Services.Security.Models.Alert;
using Infrastructure.Services.Seo;
using Infrastructure.Services.Token;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Infrastructure.Services.Security;

public static class SecurityServiceRegistration
{
    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configuration Bindings
        ConfigureSettings(services, configuration);
        
        // Service Registrations
        ConfigureSecurityServices(services);
        ConfigureMonitoringServices(services);
        ConfigureCommunicationServices(services);

        return services;
    }

    private static void ConfigureSettings(IServiceCollection services, IConfiguration configuration)
    {
        var securitySection = configuration.GetSection("Security");
        
        services.Configure<SecuritySettings>(securitySection);
        services.Configure<JwtSettings>(securitySection.GetSection("JwtSettings"));
        services.Configure<TokenSettings>(securitySection.GetSection("TokenSettings"));
        services.Configure<RateLimitConfig>(securitySection.GetSection("RateLimiting"));
        services.Configure<DDoSConfig>(securitySection.GetSection("DDoSProtection"));
        services.Configure<EmailConfig>(configuration.GetSection("Email"));
        services.Configure<AlertSettings>(configuration.GetSection("Monitoring:Alerts"));
    }
    

    private static void ConfigureSecurityServices(IServiceCollection services)
    {
        services.AddSingleton<JwtConfiguration>();
        services.AddSingleton<IKeyVaultInitializationService, KeyVaultInitializationService>();
        services.AddSingleton<ICacheEncryptionService, CacheEncryptionService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IKeyVaultService, KeyVaultService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<ITokenSettingsService, TokenSettingsService>();
        services.AddScoped<ILogService, LogService>();
        services.AddScoped<IRateLimitService, RateLimitService>();
    }

    private static void ConfigureMonitoringServices(IServiceCollection services)
    {
        services.AddSingleton<IMetricsService, PrometheusMetricsService>();
    }

    private static void ConfigureCommunicationServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAlertService, AlertService>();
    }

    public static IApplicationBuilder UseSecurityMiddleware(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        var loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("SecurityMiddleware");

        // Security Headers
        app.UseMiddleware<SecurityHeadersMiddleware>();
    
        // DDoS Protection
        if (configuration.GetValue<bool>("Security:DDoSProtection:Enabled", true))
        {
            app.UseMiddleware<DDoSProtectionMiddleware>();
            logger.LogInformation("DDoS Protection middleware enabled");
        }
    
        // Rate Limiting
        if (configuration.GetValue<bool>("Security:RateLimiting:Enabled", true))
        {
            app.UseMiddleware<RateLimitingMiddleware>();
            logger.LogInformation("Rate Limiting middleware enabled");
        }
    
        // Monitoring
        app.UseMiddleware<RequestTimingMiddleware>();
        app.UseMiddleware<AdvancedMetricsMiddleware>();

        app.UseMiddleware<ImageOptimizationMiddleware>();

        return app;
    }
}