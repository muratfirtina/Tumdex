using Core.Persistence.Repositories;

namespace Domain;

public class NewsletterLog : Entity<string>
{
    public NewsletterLog() : base("NewsletterLog") { }
    
    public string NewsletterType { get; set; } // "Monthly", "Special", etc.
    public DateTime SentDate { get; set; }
    public string EmailContent { get; set; }
    public int TotalRecipients { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
}