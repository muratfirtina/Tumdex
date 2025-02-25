using Domain.Enum;

namespace Domain.Model;

public class OrderChanges
{
    public OrderStatus? PreviousStatus { get; set; }
    public decimal? PreviousTotalPrice { get; set; }
    public string? PreviousAdminNote { get; set; }
    public List<OrderItem> PreviousItems { get; set; } = new();
}