using Application.Features.Orders.Dtos;
using Domain;
using Domain.Enum;

namespace Application.Events.OrderEvetns;


public class OrderUpdatedEvent
{
    public string? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public OrderStatus? OriginalStatus { get; set; }
    public OrderStatus? UpdatedStatus { get; set; }
    public decimal? OriginalTotalPrice { get; set; }
    public decimal? UpdatedTotalPrice { get; set; }
    public string? AdminNote { get; set; }
    public List<OrderItemUpdateDto>? UpdatedItems { get; set; }
}