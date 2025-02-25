using Application.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.BackgroundJobs;

public class MonthlyNewsletterJob : IJob
{
    private readonly INewsletterService _newsletterService;
    private readonly ILogger<MonthlyNewsletterJob> _logger;

    public MonthlyNewsletterJob(
        INewsletterService newsletterService,
        ILogger<MonthlyNewsletterJob> logger)
    {
        _newsletterService = newsletterService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _newsletterService.SendMonthlyNewsletterAsync();
            _logger.LogInformation("Monthly newsletter sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send monthly newsletter");
            throw;
        }
    }
}