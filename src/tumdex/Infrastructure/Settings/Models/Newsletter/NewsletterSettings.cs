namespace Infrastructure.Settings.Models.Newsletter;

public class NewsletterSettings
{
    public NewsletterSendTimeConfig SendTime { get; set; }
    public NewsletterThrottlingConfig Throttling { get; set; }
    public NewsletterTemplateConfig Templates { get; set; }
    public NewsletterDefaultImagesConfig DefaultImages { get; set; }
}