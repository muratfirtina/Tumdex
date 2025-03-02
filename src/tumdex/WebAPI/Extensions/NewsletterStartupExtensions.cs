using Infrastructure.BackgroundJobs;

namespace WebAPI.Extensions;

public static class NewsletterStartupExtensions
{
    public static async Task InitializeNewsletterScheduler(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Initializing newsletter scheduler");
            var scheduler = scope.ServiceProvider.GetRequiredService<NewsletterScheduler>();
            await scheduler.ScheduleNewsletterJobs();
            logger.LogInformation("Newsletter scheduler initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing newsletter scheduler");
        }
    }
}