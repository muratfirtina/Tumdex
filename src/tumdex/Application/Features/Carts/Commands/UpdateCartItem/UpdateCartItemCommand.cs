using Application.Consts;
using Application.Events.OrderEvetns;
using Application.Repositories;
using Application.Services;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Carts.Commands.UpdateCartItem;

public class UpdateCartItemCommand : IRequest<UpdateCartItemResponse>, ICacheRemoverRequest
{
    public string CartItemId { get; set; }
    public bool IsChecked { get; set; } // Güncellenecek durum

    // ICacheRemoverRequest implementation
    public string CacheKey => ""; // Komut, grup bazlı temizleme
    public bool BypassCache => false;
    // Kullanıcının sepetini etkilediği için UserCarts grubunu temizle.
    public string? CacheGroupKey => CacheGroups.UserCarts;


    // --- Handler ---
    public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, UpdateCartItemResponse>
    {
        private readonly ICartService _cartService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ICartItemRepository _cartItemRepository; // Event için CartItem bilgisi gerekebilir
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UpdateCartItemCommandHandler> _logger; // Logger eklendi

        public UpdateCartItemCommandHandler(
            ICartService cartService,
            IPublishEndpoint publishEndpoint,
            ICartItemRepository cartItemRepository,
            ICurrentUserService currentUserService,
            ILogger<UpdateCartItemCommandHandler> logger) // Logger eklendi
        {
            _cartService = cartService;
            _publishEndpoint = publishEndpoint;
            _cartItemRepository = cartItemRepository;
            _currentUserService = currentUserService;
            _logger = logger; // Atandı
        }

        public async Task<UpdateCartItemResponse> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
        {
            string userId = "unknown";
            try
            {
                 userId = await _currentUserService.GetCurrentUserIdAsync();
                 _logger.LogInformation("User {UserId} attempting to update IsChecked={IsChecked} for CartItem {CartItemId}.", userId, request.IsChecked, request.CartItemId);

                 // 1. CartService ile güncelleme yap
                 // UpdateCartItemDto'nun doğru alanları içerdiğinden emin ol.
                 await _cartService.UpdateCartItemAsync(new()
                 {
                     CartItemId = request.CartItemId,
                     IsChecked = request.IsChecked
                     // CartService içindeki DTO Quantity gibi başka alanlar bekliyorsa,
                     // bu komutun amacı sadece IsChecked ise, CartService'te ayrı bir metod daha iyi olabilir.
                     // Veya mevcut DTO'yu kullanıp Quantity'yi null bırakmak.
                 });
                 _logger.LogDebug("CartItem {CartItemId} IsChecked status updated to {IsChecked} for user {UserId}.", request.CartItemId, request.IsChecked, userId);

                 // 2. Event yayınlama (opsiyonel)
                 var cartItem = await _cartItemRepository.GetAsync(
                     predicate: ci => ci.Id == request.CartItemId,
                     include: i => i.Include(ci => ci.Cart), // CartId ve UserId için
                     cancellationToken: cancellationToken);

                 if (cartItem?.Cart != null)
                 {
                    await _publishEndpoint.Publish(new CartUpdatedEvent
                    {
                        ProductId = cartItem.ProductId,
                        Quantity = cartItem.Quantity,
                        CartId = cartItem.CartId,
                        UserId = cartItem.Cart.UserId, // DB'den gelen UserId daha güvenilir
                        CartItemId = cartItem.Id,
                        IsChecked = request.IsChecked // Event'e eklenebilir
                    }, cancellationToken);
                    _logger.LogInformation("CartUpdatedEvent published after IsChecked update for CartItem {CartItemId}.", cartItem.Id);
                 } else {
                     _logger.LogWarning("Could not find CartItem or associated Cart for publishing event. CartItemId: {CartItemId}", request.CartItemId);
                 }

                 return new UpdateCartItemResponse();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error updating IsChecked status for CartItem {CartItemId} for user {UserId}.", request.CartItemId, userId);
                 throw;
            }
        }
    }
}
