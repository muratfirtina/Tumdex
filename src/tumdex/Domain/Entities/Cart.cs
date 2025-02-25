using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain;

public class Cart: Entity<string>
{
    public string UserId { get; set; }
    public AppUser User { get; set; }
    public Order Order { get; set; }
    public ICollection<CartItem?> CartItems { get; set; }
    public Cart() : base("Cart")
    {
        CartItems = new List<CartItem?>();
    }
}