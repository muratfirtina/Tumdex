using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Infrastructure.BackgroundJobs;
using Application.Services;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Configuration;

public static class NewsletterConfiguration
{
    public static void AddNewsletterServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Get newsletter configuration
        var newsletterConfig = configuration.GetSection("Newsletter:SendTime");
        int dayOfMonth = newsletterConfig.GetValue<int>("DayOfMonth");
        int hour = newsletterConfig.GetValue<int>("Hour");
        int minute = newsletterConfig.GetValue<int>("Minute");
        
        // Register background job for scheduled newsletter sending
        services.AddQuartz(q =>
        {
            // Define the job
            var jobKey = new JobKey("MonthlyNewsletterJob");
            q.AddJob<MonthlyNewsletterJob>(opts => opts.WithIdentity(jobKey));

            // Create a trigger with cron schedule for specified day and time
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("MonthlyNewsletterTrigger")
                .WithCronSchedule($"0 {minute} {hour} {dayOfMonth} * ?"));  // Run at specific day of month
        });
        
        // Configure Quartz hosted service
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
    }
}