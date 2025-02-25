namespace Application.Features.Orders.Dtos;

public class CreateOrder
{
    public string CartId { get; set; }
    public string Description { get; set; }
    public string Address { get; set; }
}