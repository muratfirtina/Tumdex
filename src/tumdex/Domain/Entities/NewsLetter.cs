using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain;

public class Newsletter : Entity<string>
{
    public Newsletter() : base("Newsletter") { }
    
    public string Email { get; set; }
    public bool IsSubscribed { get; set; } = true;
    public DateTime SubscriptionDate { get; set; } = DateTime.UtcNow;
    public DateTime? UnsubscriptionDate { get; set; }
    public string? Source { get; set; } // "Registration", "Manual", etc.
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
}