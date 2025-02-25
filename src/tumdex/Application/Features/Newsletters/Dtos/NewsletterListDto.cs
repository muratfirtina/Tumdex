namespace Application.Features.Newsletters.Dtos;

public class NewsletterListDto
{
    public string Email { get; set; }
    public bool IsSubscribed { get; set; }
    public DateTime SubscriptionDate { get; set; }
    public DateTime? UnsubscriptionDate { get; set; }
    public string? Source { get; set; }
}