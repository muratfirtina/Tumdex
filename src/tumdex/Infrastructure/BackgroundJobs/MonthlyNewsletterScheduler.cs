using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.BackgroundJobs;

public class MonthlyNewsletterScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MonthlyNewsletterScheduler> _logger;

    public MonthlyNewsletterScheduler(
        ISchedulerFactory schedulerFactory,
        IConfiguration configuration,
        ILogger<MonthlyNewsletterScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ScheduleNewsletterJobs()
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            // Konfigürasyondan newsletter gönderim zamanını al
            var dayOfMonth = _configuration.GetValue<int>("Newsletter:SendTime:DayOfMonth", 5);
            var hour = _configuration.GetValue<int>("Newsletter:SendTime:Hour", 5);
            var minute = _configuration.GetValue<int>("Newsletter:SendTime:Minute", 0);

            _logger.LogInformation("Scheduling monthly newsletter job to run on day {Day} at {Hour}:{Minute}", 
                dayOfMonth, hour, minute);

            // Job ve trigger için anahtar tanımları
            var jobKey = new JobKey("MonthlyNewsletterJob", "NewsletterGroup");
            var triggerKey = new TriggerKey("DailyNewsletterTrigger", "NewsletterGroup");
            
            // Eğer zaten trigger varsa sil
            if (await scheduler.CheckExists(triggerKey))
            {
                await scheduler.UnscheduleJob(triggerKey);
                _logger.LogInformation("Unscheduled existing newsletter trigger");
            }
            
            // Eğer zaten bir job varsa sil
            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
                _logger.LogInformation("Deleted existing newsletter job");
            }

            // Job oluştur
            var job = JobBuilder.Create<MonthlyNewsletterJob>()
                .WithIdentity(jobKey)
                .WithDescription("Sends monthly newsletter to subscribers")
                .StoreDurably()
                .Build();
                
            // Trigger oluştur
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .WithCronSchedule($"0 {minute} {hour} * * ?") // Her gün belirtilen saatte çalışır
                .WithDescription($"Trigger for monthly newsletter (fires daily at {hour}:{minute:D2})")
                .Build();

            // Önce job'ı kaydet
            await scheduler.AddJob(job, true);
            
            // Sonra trigger ile zamanla
            await scheduler.ScheduleJob(trigger);
            
            _logger.LogInformation("Newsletter job scheduled successfully to run daily at {Hour}:{Minute:D2}", 
                hour, minute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule newsletter job");
            throw;
        }
    }
}