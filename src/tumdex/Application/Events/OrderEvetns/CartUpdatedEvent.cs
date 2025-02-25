namespace Application.Events.OrderEvetns;

public class CartUpdatedEvent
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }
    public string CartId { get; set; }
    public string UserId { get; set; }
    public string CartItemId { get; set; } // Yeni eklendi
}