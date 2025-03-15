using Application.Features.Orders.Dtos;
using Application.Features.UserAddresses.Dtos;
using Domain.Enum;

namespace Application.Abstraction.Services.Email;

/// <summary>
/// Sipariş işlemleri için e-posta gönderim servisi.
/// </summary>
public interface IOrderEmailService : IEmailService
{
    /// <summary>
    /// Oluşturulan sipariş için onay e-postası gönderir.
    /// </summary>
    Task SendCreatedOrderEmailAsync(
        string to,
        string orderCode,
        string orderDescription,
        UserAddressDto? orderAddress,
        DateTime orderCreatedDate,
        string userName,
        List<OrderItemDto> orderCartItems,
        decimal? orderTotalPrice);
    
    /// <summary>
    /// Sipariş güncelleme bildirimi gönderir.
    /// </summary>
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
