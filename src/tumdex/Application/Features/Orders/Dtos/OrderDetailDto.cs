namespace Application.Features.Orders.Dtos;

public class OrderDetailDto
{
    public string OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal? TotalPrice { get; set; }
    public string Status { get; set; }
    public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
    public string UserName { get; set; }
    public string Address { get; set; }
    public string Description { get; set; }
    public string PhoneNumber { get; set; }
}