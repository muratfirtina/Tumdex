using Application.Abstraction.Services;
using Application.Services;
using Infrastructure.BackgroundJobs;
using Infrastructure.Settings.Models.Newsletter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.Services.Mail;

/// <summary>
/// Tüm e-posta servislerinin kayıt işlemlerini yöneten sınıf
/// </summary>
public static class EmailServiceRegistration
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        // BaseEmailService alt sınıflarının kayıtları
        RegisterBaseEmailServices(services);
        
        // Newsletter servisleri
        RegisterNewsletterServices(services, configuration);
        
        return services;
    }
    
    /// <summary>
    /// Ana e-posta servislerini kaydeder (Account, Order vb.)
    /// </summary>
    private static void RegisterBaseEmailServices(IServiceCollection services)
    {
        // Hesap e-postaları servisi
        services.AddScoped<IAccountEmailService, AccountEmailService>();
        
        // Sipariş e-postaları servisi
        services.AddScoped<IOrderEmailService, OrderEmailService>();
        
        // Varsayılan e-posta servisi olarak AccountEmailService'i kullan
        services.AddScoped<IEmailService>(sp => sp.GetRequiredService<IAccountEmailService>());
    }
    
    /// <summary>
    /// Bülten servislerini ve ilgili işleri kaydeder
    /// </summary>
    private static void RegisterNewsletterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Bülten konfigürasyonu
        services.Configure<NewsletterSettings>(configuration.GetSection("Newsletter"));
        
        // Bülten e-posta servisi
        services.AddScoped<INewsletterEmailService, NewsletterEmailService>();
        
        // Bülten yönetim servisi
        services.AddScoped<INewsletterService, NewsletterService>();
        
        // E-posta kuyruğu servisi
        services.AddScoped<IEmailQueueService, EmailQueueService>();
        services.AddHostedService<EmailQueueWorker>();
        
        // Arka plan görev kuyruğu
        services.AddSingleton<IBackgroundTaskQueue>(sp => 
        {
            var logger = sp.GetRequiredService<ILogger<BackgroundTaskQueue>>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new BackgroundTaskQueue(100, logger, scopeFactory);
        });

        // QueuedHostedService kaydı
        services.AddHostedService<QueuedHostedService>();
        
        // Quartz zamanlanmış görevler
        ConfigureNewsletterJobs(services);
        
        // Newsletter scheduler
        services.AddTransient<MonthlyNewsletterScheduler>();
    }
    
    /// <summary>
    /// Bülten ile ilgili zamanlanmış görevleri yapılandırır
    /// </summary>
    private static void ConfigureNewsletterJobs(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            // Aylık bülten gönderimi işi
            var jobKey = new JobKey("MonthlyNewsletterJob", "NewsletterGroup");
            q.AddJob<MonthlyNewsletterJob>(opts =>
                opts.WithIdentity(jobKey)
                .WithDescription("Sends monthly newsletter to subscribers")
                .StoreDurably());
            
            // Basitleştirilmiş Quartz yapılandırması
            q.UseInMemoryStore();
            
            // İşlem havuzu yapılandırması
            q.UseDefaultThreadPool(tp => { tp.MaxConcurrency = 10; });
        });
        
        // Quartz hosted service
        services.AddQuartzHostedService(q => { q.WaitForJobsToComplete = true; });
    }
}