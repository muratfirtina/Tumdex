using System.Text;
using Application;
using Application.Services;
using dotenv.net;
using HealthChecks.UI.Client;
using Infrastructure;
using Infrastructure.BackgroundJobs;
using Infrastructure.Configuration;
using Infrastructure.Middleware.Security;
using Infrastructure.Services.Security;
using Infrastructure.Services.Seo;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Persistence;
using Persistence.Context;
using Persistence.DbConfiguration;
using Persistence.Services;
using Prometheus;
using Serilog;
using SignalR;
using WebAPI.Extensions;


var builder = WebApplication.CreateBuilder(args);

try
{
    DotEnv.Load();
    
    // Environment variables'ları Configuration'a ekle
    builder.Configuration.AddEnvironmentVariables();
    // 1. Temel Yapılandırmalar
    builder.AddKeyVaultConfiguration();
    builder.AddLoggingConfiguration();

    // 2. Servis Katmanları
    ConfigureServiceLayers(builder);

    // 3. Middleware ve Güvenlik
    ConfigureSecurityAndAuth(builder);
    
    // 4. Diğer Servisler
    ConfigureAdditionalServices(builder);

    var app = builder.Build();
    
    var initService = app.Services.GetRequiredService<IKeyVaultInitializationService>();
    await initService.InitializeAsync();
    await ConfigureApplication(app);
    
    // Newsletter scheduler'ı başlat
    Log.Information("Initializing newsletter scheduler");
    try
    {
        using var scope = app.Services.CreateScope();
        var newsletterScheduler = scope.ServiceProvider.GetRequiredService<MonthlyNewsletterScheduler>();
        await newsletterScheduler.ScheduleNewsletterJobs();
        Log.Information("Newsletter scheduler initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while initializing newsletter scheduler");
    }
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Service Layer Configuration Methods
void ConfigureServiceLayers(WebApplicationBuilder builder)
{
    // 1. Temel servisler (logging, configuration)
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();

    // 2. Infrastructure Layer (Key Vault ve temel servisler)
    builder.Services.AddInfrastructureServices(builder.Configuration);
    
    // 3. Security Layer (JWT ve authentication)
    builder.Services.AddSecurityServices(builder.Configuration);

    // 4. Application Layer
    builder.Services.AddApplicationServices();

    // 5. Persistence Layer
    var loggerFactory = builder.Services.BuildServiceProvider()
        .GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("DatabaseConfiguration");
    
    builder.Services.AddPersistenceServices(
        DatabaseConfiguration.GetConnectionString(builder.Configuration, logger),
        builder.Environment.IsDevelopment());

    // 6. SignalR Services
    builder.Services.AddSignalRServices();
    builder.Services.AddDistributedMemoryCache(); 
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

// Security and Authentication Configuration
void ConfigureSecurityAndAuth(WebApplicationBuilder builder)
{
    // CORS Configuration
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(builder.Configuration.GetSection("WebAPIConfiguration:AllowedOrigins").Get<string[]>())
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(
                    "Content-Security-Policy",
                    "X-Content-Type-Options",
                    "X-Frame-Options",
                    "X-XSS-Protection",
                    "Strict-Transport-Security",
                    "Referrer-Policy",
                    "Permissions-Policy"
                )
                .AllowCredentials();
        });
    });

    // Message Broker Configuration
    builder.Services.AddMessageBrokerServices(builder.Configuration);

    // Authentication Configuration
    builder.Services.AddAuthenticationServices(builder.Configuration);
}

// Additional Services Configuration
void ConfigureAdditionalServices(WebApplicationBuilder builder)
{
    // File Provider Configuration
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".avif"] = "image/avif";

    // Prometheus Metrics
    builder.Services.AddMetricServer(options => { options.Port = 9100; });
}

// Application Configuration
async Task ConfigureApplication(WebApplication app)
{
    // Environment specific configuration
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseHsts();
    app.UseHttpsRedirection();
    app.UseExceptionHandler("/Error");
    
    // Global exception handler
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            
            var exceptionHandlerPathFeature = 
                context.Features.Get<IExceptionHandlerPathFeature>();

            var error = new
            {
                StatusCode = 500,
                Message = "An internal server error occurred."
            };

            await context.Response.WriteAsJsonAsync(error);
            
            // Log the actual error
            var logger = context.RequestServices
                .GetRequiredService<ILogger<Program>>();
            
            logger.LogError(exceptionHandlerPathFeature?.Error, 
                "An unhandled exception occurred.");
        });
    });

    // Security Middleware
    app.UseSecurityMiddleware(app.Configuration);

    // Basic Middleware
    app.UseMetricServer();
    app.UseCors();
    
    app.UseMiddleware<ImageOptimizationMiddleware>();
    app.UseMiddleware<TokenValidationMiddleware>();
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = new FileExtensionContentTypeProvider
        {
            Mappings = { [".avif"] = "image/avif" ,
                [".webp"] = "image/webp" ,
                [".svg"] = "image/svg+xml" ,
                [".heic"] = "image/heic",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png"
            }
        }
        
    });
    
    

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Request Logging
    app.UseCustomRequestLogging();

    // API Routes
    app.MapControllers();
    app.MapHubs();

    // Health Checks
    ConfigureHealthChecks(app);

    // Database Migration
    await using (var scope = app.Services.CreateAsyncScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<TumdexDbContext>();
            await context.Database.MigrateAsync();
            await RoleAndUserSeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred during database initialization");
            throw;
        }
    }
}

// Health Checks Configuration
void ConfigureHealthChecks(WebApplication app)
{
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
}