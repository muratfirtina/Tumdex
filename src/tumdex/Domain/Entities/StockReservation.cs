using Core.Persistence.Repositories;

namespace Domain;

public class StockReservation : Entity<string>
{
    public string ProductId { get; set; }
    public string CartItemId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpirationTime { get; set; }
    public bool IsActive { get; set; }

    public StockReservation() : base("StockReservation")
    {
        
    }
}