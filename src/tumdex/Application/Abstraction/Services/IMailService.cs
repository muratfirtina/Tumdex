using Application.Features.Orders.Dtos;
using Application.Features.UserAddresses.Dtos;
using Domain;
using Domain.Enum;

namespace Application.Abstraction.Services;

public interface IMailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = true);
    Task SendEmailAsync(string[] tos, string subject, string body, bool isBodyHtml = true);
    Task SendPasswordResetEmailAsync(string to,string userId, string resetToken);
    Task SendCreatedOrderEmailAsync(string to, string orderCode, string orderDescription,
        UserAddressDto? orderAddress,
        DateTime orderCreatedDate, string userName, List<OrderItemDto> orderCartItems, decimal? orderTotalPrice);
    Task SendOrderUpdateNotificationAsync(
        string to,
        string? orderCode,
        string? adminNote,
        OrderStatus? originalStatus,
        OrderStatus? updatedStatus,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice,
        List<OrderItemUpdateDto>? updatedItems);

}
