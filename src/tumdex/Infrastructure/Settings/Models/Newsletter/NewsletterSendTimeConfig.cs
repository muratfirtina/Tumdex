namespace Infrastructure.Settings.Models.Newsletter;

public class NewsletterSendTimeConfig
{
    public int DayOfMonth { get; set; } = 5;
    public int Hour { get; set; } = 5;
    public int Minute { get; set; } = 0;
}