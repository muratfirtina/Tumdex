namespace Infrastructure.Settings.Models.Newsletter;

public class NewsletterThrottlingConfig
{
    public int MaxConcurrentEmails { get; set; } = 5;
    public int DelayBetweenEmails { get; set; } = 1000;  // milliseconds
}