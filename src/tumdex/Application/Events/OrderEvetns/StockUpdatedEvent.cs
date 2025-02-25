namespace Application.Events.OrderEvetns;

public class StockUpdatedEvent
{
    public string ProductId { get; set; }
    public int NewStock { get; set; }
    public int MinStockLevel { get; set; }
    public string ProductName { get; set; }
}