using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class MonthlyNewsletterJob : IJob
{
    private readonly INewsletterService _newsletterService;
    private readonly ILogger<MonthlyNewsletterJob> _logger;
    private readonly IConfiguration _configuration;

    public MonthlyNewsletterJob(
        INewsletterService newsletterService,
        ILogger<MonthlyNewsletterJob> logger,
        IConfiguration configuration)
    {
        _newsletterService = newsletterService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("Monthly newsletter job started at {Time}", DateTime.UtcNow);

            // Check if we're in the correct day of month to send the newsletter
            var sendDay = _configuration.GetValue<int>("Newsletter:SendTime:DayOfMonth", 5);

            if (DateTime.UtcNow.Day == sendDay)
            {
                _logger.LogInformation("Executing scheduled monthly newsletter sending");
                await _newsletterService.SendMonthlyNewsletterAsync();
                _logger.LogInformation("Monthly newsletter sent successfully");
            }
            else
            {
                _logger.LogInformation(
                    "Skipping newsletter send - current day {CurrentDay} doesn't match configured send day {SendDay}",
                    DateTime.UtcNow.Day, sendDay);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send monthly newsletter");

            // Re-throw the exception to let Quartz handle it according to retry policy
            throw;
        }
    }
}