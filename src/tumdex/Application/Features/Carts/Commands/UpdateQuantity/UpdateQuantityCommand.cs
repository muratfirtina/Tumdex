using Application.Consts;
using Application.Events.OrderEvetns;
using Application.Repositories;
using Application.Services;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Carts.Commands.UpdateQuantity;

public class UpdateQuantityCommand : IRequest<UpdateQuantityResponse>, ICacheRemoverRequest
{
    public string CartItemId { get; set; }
    public int Quantity { get; set; } // Yeni miktar

    // ICacheRemoverRequest implementation
    public string CacheKey => ""; // Komut, grup bazlı temizleme
    public bool BypassCache => false;
    // Kullanıcının sepetini etkilediği için UserCarts grubunu temizle.
    public string? CacheGroupKey => CacheGroups.UserCarts;

    // --- Handler ---
    public class UpdateQuantityCommandHandler : IRequestHandler<UpdateQuantityCommand, UpdateQuantityResponse>
    {
        private readonly ICartService _cartService;
        private readonly ICartItemRepository _cartItemRepository; // Event için ProductId gerekli
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IStockReservationService _stockReservationService; // Stok rezervasyonunu güncellemek için
         private readonly ICurrentUserService _currentUserService; // Loglama için
        private readonly ILogger<UpdateQuantityCommandHandler> _logger; // Logger eklendi

        public UpdateQuantityCommandHandler(
            ICartService cartService,
            ICartItemRepository cartItemRepository, // Eklendi
            IPublishEndpoint publishEndpoint,
            IStockReservationService stockReservationService,
            ICurrentUserService currentUserService, // Eklendi
            ILogger<UpdateQuantityCommandHandler> logger) // Eklendi
        {
            _cartService = cartService;
            _cartItemRepository = cartItemRepository; // Atandı
            _publishEndpoint = publishEndpoint;
            _stockReservationService = stockReservationService;
            _currentUserService = currentUserService; // Atandı
            _logger = logger; // Atandı
        }

        public async Task<UpdateQuantityResponse> Handle(UpdateQuantityCommand request, CancellationToken cancellationToken)
        {
             string userId = "unknown";
             CartItem? cartItemForEvent = null; // Event için bilgiyi sakla
            try
            {
                 userId = await _currentUserService.GetCurrentUserIdAsync();
                 _logger.LogInformation("User {UserId} attempting to update quantity to {Quantity} for CartItem {CartItemId}.", userId, request.Quantity, request.CartItemId);

                 // 0. Event için mevcut CartItem bilgisini al (güncellemeden önce)
                 // ProductId ve CartId lazım olacak.
                 cartItemForEvent = await _cartItemRepository.GetAsync(ci => ci.Id == request.CartItemId, include: ci => ci.Include(c => c.Cart));
                 if (cartItemForEvent == null)
                 {
                     _logger.LogWarning("CartItem {CartItemId} not found before quantity update.", request.CartItemId);
                     throw new Exception("Cart item not found.");
                 }


                 // 1. CartService ile miktarı güncelle
                 await _cartService.UpdateQuantityAsync(new()
                 {
                     CartItemId = request.CartItemId,
                     Quantity = request.Quantity,
                     // CartService DTO'su IsChecked gibi başka alanlar bekliyorsa handle edilmeli.
                 });
                 _logger.LogDebug("CartItem {CartItemId} quantity updated to {Quantity} via CartService for user {UserId}.", request.CartItemId, request.Quantity, userId);


                 // 2. Stok Rezervasyonunu Güncelle/Oluştur/İptal Et
                 if (request.Quantity > 0)
                 {
                     // CreateReservationAsync var olanı güncellemeli
                     await _stockReservationService.CreateReservationAsync(
                         cartItemForEvent.ProductId, // Önceki adımdan alınan ProductId
                         request.CartItemId,
                         request.Quantity); // Yeni miktar ile
                     _logger.LogDebug("Stock reservation created/updated for CartItem {CartItemId} with new quantity {Quantity}.", request.CartItemId, request.Quantity);
                 }
                 else // Quantity 0 ise rezervasyonu kaldır
                 {
                     await _stockReservationService.ReleaseReservationAsync(request.CartItemId);
                      _logger.LogDebug("Stock reservation released for CartItem {CartItemId} due to zero quantity.", request.CartItemId);
                     // Not: CartService.UpdateQuantityAsync içinde Quantity 0 ise CartItem'ı silmiş olmalı.
                     // Bu durumda rezervasyon iptali orada da yapılabilir. Burada tekrar yapmak zararsız.
                 }


                 // 3. Event Yayınla (Stok rezervasyonu sonrası)
                  if (cartItemForEvent?.Cart != null)
                  {
                    await _publishEndpoint.Publish(new CartUpdatedEvent
                    {
                        ProductId = cartItemForEvent.ProductId,
                        Quantity = request.Quantity, // Yeni miktar
                        CartId = cartItemForEvent.CartId,
                        UserId = cartItemForEvent.Cart.UserId,
                        CartItemId = request.CartItemId,
                        IsChecked = cartItemForEvent.IsChecked // Önceki IsChecked durumu (veya tekrar çekilebilir)
                    }, cancellationToken);
                    _logger.LogInformation("CartUpdatedEvent published after quantity update for CartItem {CartItemId}.", request.CartItemId);
                  } else {
                       _logger.LogWarning("Could not publish CartUpdatedEvent due to missing cart info. CartItemId: {CartItemId}", request.CartItemId);
                  }


                 return new UpdateQuantityResponse();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error updating quantity for CartItem {CartItemId} for user {UserId}.", request.CartItemId, userId);
                 // Hata durumunda stok rezervasyonunun durumu ne olacak?
                 // Eğer CartService hata fırlatırsa, rezervasyon güncellenmemiş olabilir.
                 // Bu senaryo dikkatlice ele alınmalı. Belki rezervasyonu en sona bırakmak?
                 throw;
            }
        }
    }
}