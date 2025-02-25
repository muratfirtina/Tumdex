using Application.Features.Orders.Dtos;
using Application.Features.UserAddresses.Dtos;

namespace Application.Events.OrderEvetns;

public class OrderCreatedEvent
{
    public string OrderId { get; set; }
    public string OrderCode { get; set; }
    public string Email { get; set; }
    public bool EmailSent { get; set; }
    public string UserName { get; set; }
    public DateTime OrderDate { get; set; }
    public List<OrderItemDto> OrderItems { get; set; }
    public UserAddressDto UserAddress { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Description { get; set; }
}
