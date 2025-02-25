using Application.Features.Orders.Dtos;
using Application.Features.PhoneNumbers.Dtos;
using Application.Features.UserAddresses.Dtos;

namespace Application.Features.Orders.Queries.GetUserOrderById;

public class GetUserOrderByIdQueryResponse
{
    public string OrderId { get; set; }
    public string OrderCode { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public DateTime OrderDate { get; set; }
    public UserAddressDto? UserAddress { get; set; }
    public string Description { get; set; }
    public PhoneNumberDto? PhoneNumber { get; set; }
    public decimal? TotalPrice { get; set; }
    public string Status { get; set; }
    public List<OrderItemDto> OrderItems { get; set; }
    public string Email { get; set; }
}