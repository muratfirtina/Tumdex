using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;


namespace Infrastructure.Configuration;

public static class LoggingConfiguration
{
    public static WebApplicationBuilder AddLoggingConfiguration(this WebApplicationBuilder builder)
    {
        // Key Vault'tan loglama ayarlarını al
        var seqServerUrl = builder.Configuration.GetSecretFromKeyVault("SeqServerUrl");
        var seqApiKey = builder.Configuration.GetSecretFromKeyVault("SeqApiKey");

        // Minimum log seviyesini yapılandırmadan al veya varsayılan değeri kullan
        var minimumLevel = builder.Configuration.GetValue<string>("Serilog:MinimumLevel:Default")
                ?.ToLower() switch
            {
                "debug" => LogEventLevel.Debug,
                "error" => LogEventLevel.Error,
                "fatal" => LogEventLevel.Fatal,
                "verbose" => LogEventLevel.Verbose,
                "warning" => LogEventLevel.Warning,
                _ => LogEventLevel.Information
            };

        // Serilog yapılandırması
        builder.Host.UseSerilog((context, config) =>
        {
            // Temel yapılandırma
            var logConfig = config
                .MinimumLevel.Is(minimumLevel)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "Tumdex")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);

            // Loglama hedeflerini yapılandır
            ConfigureLogTargets(logConfig, seqServerUrl, seqApiKey, context.HostingEnvironment.IsDevelopment());
        });

        return builder;
    }

    private static void ConfigureLogTargets(
        LoggerConfiguration config,
        string seqServerUrl,
        string seqApiKey,
        bool isDevelopment)
    {
        // Development ortamında renkli console çıktısı
        if (isDevelopment)
        {
            config.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
        else
        {
            // Production ortamında JSON formatında loglama
            config.WriteTo.Console(new JsonFormatter());
        }

        // Seq entegrasyonu (eğer yapılandırılmışsa)
        if (!string.IsNullOrEmpty(seqServerUrl))
        {
            config.WriteTo.Seq(
                serverUrl: seqServerUrl,
                apiKey: seqApiKey,
                restrictedToMinimumLevel: LogEventLevel.Information,
                batchPostingLimit: 50,
                period: TimeSpan.FromSeconds(5),
                bufferBaseFilename: Path.Combine(AppContext.BaseDirectory, "Logs", "seq-buffer"));
        }

        // Dosya loglaması
        var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "log.txt");
        config.WriteTo.File(
            path: logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 31,
            restrictedToMinimumLevel: LogEventLevel.Warning,
            formatProvider: System.Globalization.CultureInfo.InvariantCulture);

        // Kritik hatalar için ayrı dosya
        var criticalLogPath = Path.Combine(AppContext.BaseDirectory, "Logs", "critical-.txt");
        config.WriteTo.File(
            path: criticalLogPath,
            rollingInterval: RollingInterval.Day,
            restrictedToMinimumLevel: LogEventLevel.Error,
            formatProvider: System.Globalization.CultureInfo.InvariantCulture);
    }

    public static ILogger CreateBootstrapLogger()
    {
        // Uygulama başlangıcında kullanılacak basit logger
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();
    }

    public static IApplicationBuilder UseCustomRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "[{Timestamp:HH:mm:ss} {Level:u3}] HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);

                // Kullanıcı bilgilerini ekle
                if (httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserName", httpContext.User.Identity.Name);
                    diagnosticContext.Set("UserClaims", httpContext.User.Claims.ToDictionary(
                        c => c.Type,
                        c => c.Value
                    ));
                }

                // Performance metrikleri
                diagnosticContext.Set("RequestContentLength", httpContext.Request.ContentLength);
                diagnosticContext.Set("ResponseContentLength", httpContext.Response.ContentLength);
            };

            // Hassas verileri filtrele
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex != null)
                    return LogEventLevel.Error;
                if (httpContext.Response.StatusCode >= 500)
                    return LogEventLevel.Error;
                if (httpContext.Response.StatusCode >= 400)
                    return LogEventLevel.Warning;
                return LogEventLevel.Information;
            };
        });

        return app;
    }
}