using System.Text;
using Application;
using Application.Services;
using dotenv.net;
using HealthChecks.UI.Client;
using Infrastructure;
using Infrastructure.BackgroundJobs;
using Infrastructure.Configuration;
using Infrastructure.Middleware.Monitoring;
using Infrastructure.Middleware.Security;
using Infrastructure.Services.Monitoring.Models;
using Infrastructure.Services.Security; // SecurityServiceRegistration için
using Infrastructure.Services.Seo;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Persistence;
using Persistence.Context;
using Persistence.DbConfiguration;
using Persistence.Services;
using Prometheus;
using Serilog; // Serilog için using
using SignalR; // SignalR namespace'i using direktiflerinde olmalı
using WebAPI.Controllers;
using WebAPI.Extensions;


var builder = WebApplication.CreateBuilder(args);

try
{
    DotEnv.Load();
    builder.Configuration.AddEnvironmentVariables();
    builder.AddKeyVaultConfiguration();
    builder.AddLoggingConfiguration(); // Serilog yapılandırması burada

    ConfigureServiceLayers(builder);
    ConfigureSecurityAndAuth(builder); // CORS tanımı burada
    ConfigureAdditionalServices(builder);

    var app = builder.Build();

    // --- Uygulama Başlangıç Konfigürasyonu ---
    await ConfigureApplication(app);
    // --- Uygulama Başlangıç Konfigürasyonu Sonu ---


    await app.RunAsync();
}
catch (Exception ex)
{
    // Uygulama başlangıcında kritik hata
    // Loglama henüz tam yapılandırılmamış olabilir, Console.WriteLine güvenli
    Console.WriteLine($"Uygulama başlatılamadı: {ex}");
    // Serilog yapılandırıldıysa logla
    Log.Fatal(ex, "Uygulama başlatılırken beklenmedik bir hata oluştu.");
    // throw; // Geliştirme ortamında hatayı görmek için fırlatılabilir
    // Environment.Exit(1); // Uygulamayı hemen kapatabilir
}
finally
{
    Log.CloseAndFlush(); // Serilog buffer'ını temizle
}

//-----------------------------------------------------
// Yardımcı Yapılandırma Metotları
//-----------------------------------------------------

void ConfigureServiceLayers(WebApplicationBuilder builder)
{
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor(); // IHttpContextAccessor'ı kaydet

    builder.Services.AddInfrastructureServices(builder.Configuration); // Key Vault, Cache, Storage, Token vb.
    builder.Services.AddSecurityServices(builder.Configuration); // Antiforgery, Monitoring, Communication vb.
    builder.Services.AddApplicationServices(); // MediatR, AutoMapper, Business Rules
    builder.Services.AddPersistenceServices( // DbContext, Repositories, Identity Core
        DatabaseConfiguration.GetConnectionString(builder.Configuration, null), // Logger'ı sonra alabiliriz
        builder.Environment.IsDevelopment());
    builder.Services.AddSignalRServices(); // SignalR Core ve Hub Servisleri
    builder.Services.Configure<VisitorStatsOptions>(options => {
        options.UpdateIntervalSeconds = 15; // 15 saniyede bir güncelleme
        options.MinBroadcastIntervalSeconds = 5; // En az 5 saniye aralıkla yayın
    });

    builder.Services.AddHostedService<VisitorStatsBackgroundService>();

    // Prometheus controller için
    builder.Services.AddControllers()
        .AddApplicationPart(typeof(MetricsController).Assembly);
}

void ConfigureSecurityAndAuth(WebApplicationBuilder builder)
{
    // CORS Politikası Tanımı
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("WebAPIConfiguration:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            if (builder.Environment.IsDevelopment())
            {
                // Distinct() ile tekrarlanan eklemeleri önle
                allowedOrigins = allowedOrigins.Append("http://localhost:4200").Distinct().ToArray();
                allowedOrigins = allowedOrigins.Append("https://localhost:4200").Distinct().ToArray();
            }
            Console.WriteLine($"CORS İzin Verilen Origin'ler: {string.Join(", ", allowedOrigins)}");

            policy.WithOrigins(allowedOrigins)
                  .WithHeaders( // İzin verilen başlıklar
                      "Content-Type", "Authorization", "X-Requested-With",
                      "x-signalr-user-agent", "X-CSRF-TOKEN" // Gerekliyse ekle
                   )
                  .AllowAnyMethod() // Veya .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                  .WithExposedHeaders( // Tarayıcının okuyabileceği başlıklar
                      "Content-Security-Policy", "X-Content-Type-Options", "X-Frame-Options",
                      "X-XSS-Protection", "Strict-Transport-Security", "Referrer-Policy",
                      "Permissions-Policy" // Örnek, ihtiyaç varsa ekle
                  )
                  .AllowCredentials(); // SignalR ve token tabanlı auth için gerekli
        });
    });

    builder.Services.AddMessageBrokerServices(builder.Configuration); // RabbitMQ vb.
    builder.Services.AddAuthenticationServices(builder.Configuration); // JWT ve Identity yapılandırması
}

void ConfigureAdditionalServices(WebApplicationBuilder builder)
{
    // Prometheus Metrics - Port yapılandırmadan okunuyor
    var metricsPort = builder.Configuration.GetValue<int>("Monitoring:Metrics:Port", 9101);
    builder.Services.AddMetricServer(options => { options.Port = (ushort)metricsPort; });
    builder.Services.Configure<GoogleAnalyticsSettings>(builder.Configuration.GetSection("GoogleAnalytics"));
}

// --- Middleware Pipeline Yapılandırması ---
async Task ConfigureApplication(WebApplication app)
{
    // Ortama özel yapılandırma
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage(); // Detaylı hata sayfası
        app.UseSwagger(); // Swagger middleware'leri
        app.UseSwaggerUI();
    }
    else
    {
        app.UseExceptionHandler("/Error"); // Üretim için genel hata sayfası
        app.UseHsts(); // HTTPS Zorlama başlığı
    }

    // Middleware Sırası ÖNEMLİDİR!
    // ÖNEMLİ: ForwardedHeaders Middleware'ini HTTPS Redirection'dan önce ekleyin
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        // Eğer güvenlik sebebiyle bilinen proxy ağlarını kısıtlamak istiyorsanız:
        // Known Proxies ve Networks kısıtlamalarını kaldır
        KnownNetworks = { },
        KnownProxies = { },
        // Bu doğru IP alımı için kritik olabilir, 
        // ForwardLimit sıfır olursa X-Forwarded-For başlığı tamamen yok sayılır
        ForwardLimit = null // Tüm başlık zincirini kabul et (en güvenlisi olmayabilir)
    });

    // 1. HTTPS Yönlendirmesi (Genellikle en başta)
    app.UseHttpsRedirection();

    // 2. Serilog Request Logging (Mümkün olduğunca başta)
    app.UseSerilogRequestLogging(); // Gelen istekleri loglar
    app.AddUserNameLogging(); // Kullanıcı adını log context'ine ekler (Auth'dan sonra daha mantıklı olabilir)

    // 3. Statik Dosyalar
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".avif"] = "image/avif";
    provider.Mappings[".webp"] = "image/webp";
    provider.Mappings[".svg"] = "image/svg+xml";
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = provider });

    // 4. Routing
    app.UseRouting(); // Endpoint eşleştirmesi için gerekli

    // 5. CORS (Routing'den SONRA, Auth ve Endpoint'lerden ÖNCE)
    app.UseCors(); // Tanımlanan CORS politikasını uygular

    // 6. Authentication & Authorization (CORS'tan SONRA, Endpoint'lerden ÖNCE)
    app.UseAuthentication();
    app.UseAuthorization();

    // 7. Özel Güvenlik ve Diğer Middleware'ler
    app.UseSecurityMiddleware(app.Configuration); // DDoS, RateLimit, SecurityHeaders(CORS hariç) vb. içerir
    app.UseMiddleware<TokenValidationMiddleware>(); // Özel token doğrulama
    app.UseMiddleware<ImageOptimizationMiddleware>(); // Resim optimizasyonu

    // 8. Prometheus Metrics Server
    app.UseMetricServer(); // /metrics endpoint'ini ekler
    app.UseVisitorTracking();
    // 9. Endpoints (En sonda)
    app.MapControllers();
    app.MapHubs(); // SignalR Hub'ları (HubRegistration içindeki)
    ConfigureHealthChecks(app); // Sağlık kontrolü endpoint'leri

    // 10. Uygulama Başlangıç İşlemleri
    await InitializeDatabaseAndSeedData(app);
    await app.InitializeNewsletterScheduler(); // Newsletter
}

// Health Checks Endpoint Yapılandırması
void ConfigureHealthChecks(WebApplication app)
{
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        // Sadece 'ready' tag'ine sahip check'leri çalıştırır (DB, Cache vb.)
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        // Sadece uygulamanın canlı olup olmadığını kontrol eder (herhangi bir check çalıştırmaz)
        Predicate = _ => false,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    // app.MapHealthChecksUI(); // UI için (opsiyonel)
}

// Veritabanı Başlatma ve Seed Metodu
async Task InitializeDatabaseAndSeedData(WebApplication app)
{
    // Scope oluşturarak scoped servisleri (DbContext, UserManager vb.) al
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<TumdexDbContext>();
        logger.LogInformation("Veritabanı migration'ları uygulanıyor...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Veritabanı migration'ları başarıyla uygulandı.");

        logger.LogInformation("Başlangıç rolleri ve kullanıcıları seed ediliyor...");
        await RoleAndUserSeeder.SeedAsync(scope.ServiceProvider);
        logger.LogInformation("Başlangıç rolleri ve kullanıcıları başarıyla seed edildi.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabanı başlatma (migration veya seeding) sırasında bir hata oluştu.");
        // Uygulamanın bu durumda devam etmesi riskli olabilir.
        // Geliştirme ortamında hatayı fırlatmak daha iyi olabilir:
        if (app.Environment.IsDevelopment()) throw;
    }
}