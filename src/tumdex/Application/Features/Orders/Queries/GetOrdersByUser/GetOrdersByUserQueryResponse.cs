using Application.Features.Orders.Dtos;
using Application.Features.PhoneNumbers.Dtos;
using Application.Features.UserAddresses.Dtos;
using Core.Application.Responses;
using Domain.Enum;

namespace Application.Features.Orders.Queries.GetOrdersByUser;

public class GetOrdersByUserQueryResponse :IResponse
{
    public string Id { get; set; }
    public DateTime OrderDate { get; set; }
    public string OrderCode { get; set; }
    public decimal? TotalPrice { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
    public string UserName { get; set; }
    public UserAddressDto UserAddress { get; set; }  // Use the DTO instead
    public string Description { get; set; }
    public PhoneNumberDto PhoneNumber { get; set; }
}
