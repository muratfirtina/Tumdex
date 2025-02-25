using System.Security.Claims;
using Domain.Identity;
using Infrastructure.Services.Security.JWT;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Persistence.Context;

namespace Infrastructure.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ConfigureIdentity(services);
        
        // JWT servislerini yapılandır
        services.AddScoped<IJwtService, JwtService>();
        
        // JWT yapılandırmasını singleton olarak kaydet
        services.AddSingleton<IJwtConfiguration>(serviceProvider =>
        {
            // JWT servisini geçici scope'ta al
            using var scope = serviceProvider.CreateScope();
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            
            // İlk yapılandırmayı senkron olarak al
            return jwtService.GetJwtConfigurationAsync()
                .GetAwaiter()
                .GetResult();
        });

        ConfigureJwtAuthentication(services);
        
        return services;
    }

    private static void ConfigureIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequiredLength = 3;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.User.RequireUniqueEmail = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<TumdexDbContext>()
        .AddDefaultTokenProviders()
        .AddSignInManager<SignInManager<AppUser>>();
    }

    private static void ConfigureJwtAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer("Admin", options =>
        {
            // Geçici service provider oluştur
            using var serviceProvider = services.BuildServiceProvider();
            var jwtConfig = serviceProvider.GetRequiredService<IJwtConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtAuthentication");

            // Token doğrulama parametrelerini ayarla
            options.TokenValidationParameters = jwtConfig.TokenValidationParameters;

            options.Events = new JwtBearerEvents
            {
                // WebSocket bağlantıları için token yönetimi
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && 
                        path.StartsWithSegments("/order-hub"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },

                // Token doğrulama hataları için
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Add("Token-Expired", "true");
                        logger.LogWarning("Token süresi dolmuş");
                    }
                    else
                    {
                        logger.LogError(context.Exception, "Token doğrulama hatası");
                    }

                    return Task.CompletedTask;
                },

                // Başarılı token doğrulaması için
                OnTokenValidated = context =>
                {
                    logger.LogInformation(
                        "Token başarıyla doğrulandı - Kullanıcı: {UserName}", 
                        context.Principal?.Identity?.Name ?? "Unknown"
                    );
                    return Task.CompletedTask;
                }
            };
        });
    }
}