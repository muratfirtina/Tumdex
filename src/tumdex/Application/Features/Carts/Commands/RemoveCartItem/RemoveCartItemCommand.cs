using Application.Consts;
using Application.Services;
using Core.Application.Pipelines.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Carts.Commands.RemoveCartItem;

public class RemoveCartItemCommand : IRequest<RemoveCartItemResponse>, ICacheRemoverRequest
{
    public string CartItemId { get; set; }

    // ICacheRemoverRequest implementation
    public string CacheKey => ""; // Komut, grup bazlı temizleme
    public bool BypassCache => false;
    // Kullanıcının sepetini etkilediği için UserCarts grubunu temizle.
    public string? CacheGroupKey => CacheGroups.UserCarts;

    // --- Handler ---
    public class RemoveCartItemRequestHandler : IRequestHandler<RemoveCartItemCommand, RemoveCartItemResponse>
    {
        private readonly ICartService _cartService;
        private readonly IStockReservationService _stockReservationService; // Rezervasyonu iptal etmek için
        private readonly ICurrentUserService _currentUserService; // Loglama için
        private readonly ILogger<RemoveCartItemRequestHandler> _logger; // Logger eklendi

        public RemoveCartItemRequestHandler(
            ICartService cartService,
            IStockReservationService stockReservationService, // Eklendi
            ICurrentUserService currentUserService, // Eklendi
            ILogger<RemoveCartItemRequestHandler> logger) // Eklendi
        {
            _cartService = cartService;
            _stockReservationService = stockReservationService; // Atandı
            _currentUserService = currentUserService; // Atandı
            _logger = logger; // Atandı
        }

        public async Task<RemoveCartItemResponse> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
        {
            string userId = "unknown";
            try
            {
                 userId = await _currentUserService.GetCurrentUserIdAsync();
                 _logger.LogInformation("User {UserId} attempting to remove CartItem {CartItemId} from cart.", userId, request.CartItemId);

                 // 1. Stok rezervasyonunu iptal et (varsa)
                 // Bu işlem CartService.RemoveCartItemAsync içinde de yapılabilir, burada yapmak daha explicit.
                 // ReleaseReservationAsync metodu, rezervasyon yoksa hata vermemeli.
                 //await _stockReservationService.ReleaseReservationAsync(request.CartItemId);
                 //_logger.LogDebug("Stock reservation released (if existed) for CartItem {CartItemId}.", request.CartItemId);

                 // 2. Sepetten ürünü kaldır
                 await _cartService.RemoveCartItemAsync(request.CartItemId);
                 _logger.LogInformation("CartItem {CartItemId} removed from cart for user {UserId}.", request.CartItemId, userId);

                 // 3. Event yayınlama (opsiyonel, gerekirse eklenebilir)
                 // await _publishEndpoint.Publish(new CartItemRemovedEvent { ... });

                 return new RemoveCartItemResponse();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error removing CartItem {CartItemId} from cart for user {UserId}.", request.CartItemId, userId);
                 throw;
            }
        }
    }
}