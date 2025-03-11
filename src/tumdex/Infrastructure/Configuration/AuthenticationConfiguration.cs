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
        // AddIdentityCore yerine AddIdentity kullanın
        services.AddIdentity<AppUser, AppRole>(options =>
            {
                // Şifre politikası
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;

                // Kullanıcı politikası
                options.User.RequireUniqueEmail = true;

                // Hesap kilitleme politikası
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // SignIn ayarları
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedAccount = true;
            })
            .AddEntityFrameworkStores<TumdexDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<DataProtectorTokenProvider<AppUser>>(TokenOptions.DefaultProvider)
            .AddTokenProvider<EmailTokenProvider<AppUser>>("Email")
            .AddTokenProvider<PhoneNumberTokenProvider<AppUser>>("Phone")
            .AddSignInManager<SignInManager<AppUser>>();
            // Cookie kimlik doğrulama eklenmesi

            
        // SignInManager için gerekli cookie authentication
        services.ConfigureApplicationCookie(options => 
        {
            options.Cookie.Name = "Tumdex.Identity";
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            
            // API odaklı uygulama olduğu için login/logout path'leri ayarlanmamış
            options.LoginPath = "/api/auth/login"; // Bu yolu kendinizdeki uygun bir route ile değiştirin
            options.LogoutPath = "/api/auth/logout"; // Bu yolu kendinizdeki uygun bir route ile değiştirin
            
            // API için bu önemli - 401 yerine 302 redirect yapma
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });
    }

    private static void ConfigureJwtAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            // Önce Identity cookie, sonra JWT ile doğrulama yapacak şekilde ayarla
            options.DefaultScheme = IdentityConstants.ApplicationScheme; // Önemli değişiklik
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            
            // Authentication şemasını ÖNCE cookie sonra JWT kontrol edecek şekilde ayarla
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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