using Application.Events.OrderEvetns;
using Application.Features.Carts.Dtos;
using Application.Services;
using Core.Application.Pipelines.Caching;
using MassTransit;
using MediatR;
using System.Text.Json;
using Application.Consts;
using Microsoft.Extensions.Logging;

namespace Application.Features.Carts.Commands.AddItemToCart;

// CreateCartCommand - Yeni yapıya uygun olarak düzenlenmiş
public class CreateCartCommand : IRequest<CreatedCartResponse>, ICacheRemoverRequest // ITransactionalRequest eklenebilir
{
    public CreateCartItemDto CreateCartItem { get; set; }

    // ICacheRemoverRequest implementation
    public string CacheKey => ""; // Komut, grup bazlı temizleme
    public bool BypassCache => false;
    // Kullanıcının sepetini etkilediği için UserCarts grubunu temizle.
    public string? CacheGroupKey => CacheGroups.UserCarts;

    // Constructor
    public CreateCartCommand(CreateCartItemDto createCartItem)
    {
        CreateCartItem = createCartItem;
    }

    // --- Handler ---
    public class CreateCartCommandHandler : IRequestHandler<CreateCartCommand, CreatedCartResponse>
    {
        private readonly ICartService _cartService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IStockReservationService _stockReservationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<CreateCartCommandHandler> _logger; // Logger eklendi

        public CreateCartCommandHandler(
            ICartService cartService,
            IPublishEndpoint publishEndpoint,
            IStockReservationService stockReservationService,
            ICurrentUserService currentUserService,
            ILogger<CreateCartCommandHandler> logger) // Logger eklendi
        {
            _cartService = cartService;
            _publishEndpoint = publishEndpoint;
            _stockReservationService = stockReservationService;
            _currentUserService = currentUserService;
            _logger = logger; // Atandı
        }

        public async Task<CreatedCartResponse> Handle(CreateCartCommand request, CancellationToken cancellationToken)
        {
            string userId = "unknown";
            try
            {
                userId = await _currentUserService.GetCurrentUserIdAsync();
                _logger.LogInformation("User {UserId} attempting to add product {ProductId} (Qty: {Quantity}) to cart.",
                    userId, request.CreateCartItem.ProductId, request.CreateCartItem.Quantity);

                // 1. Sepete ürünü ekle (CartService içindeki kontrollerle birlikte)
                await _cartService.AddItemToCartAsync(request.CreateCartItem);
                _logger.LogDebug("Product {ProductId} added/updated in cart for user {UserId}.", request.CreateCartItem.ProductId, userId);

                // 2. Yeni eklenen/güncellenen CartItem'ı al (Stok rezervasyonu ve event için ID gerekli)
                 // Not: AddItemToCartAsync sonrası CartItem'ı tekrar çekmek yerine,
                 // AddItemToCartAsync'in CartItem'ı döndürmesi daha verimli olabilir.
                 // Şimdilik tekrar çekiyoruz.
                var cart = await _cartService.GetUserActiveCart(); // En güncel sepeti al
                var cartItem = cart?.CartItems?.FirstOrDefault(ci => ci.ProductId == request.CreateCartItem.ProductId);

                if (cartItem == null)
                {
                    _logger.LogError("Could not find the CartItem for Product {ProductId} immediately after adding for user {UserId}.", request.CreateCartItem.ProductId, userId);
                    // Bu durum beklenmemeli, hata fırlatılabilir veya loglanıp devam edilebilir.
                    throw new Exception("Failed to retrieve cart item after adding to cart.");
                }
                 _logger.LogDebug("Retrieved CartItem ID: {CartItemId} for Product {ProductId}", cartItem.Id, cartItem.ProductId);


                // 3. Stok rezervasyonu oluştur/güncelle
                 // CreateReservationAsync metodu, var olan rezervasyonu güncellemeli veya yenisini oluşturmalı.
                await _stockReservationService.CreateReservationAsync(
                    request.CreateCartItem.ProductId,
                    cartItem.Id, // CartItem ID'si ile rezervasyon
                    cartItem.Quantity // Güncel miktar ile rezervasyon
                );
                _logger.LogDebug("Stock reservation created/updated for CartItem {CartItemId}.", cartItem.Id);


                // 4. Event yayınla (Stok rezervasyonu sonrası)
                 if (cart != null) // cart null olmamalı ama kontrol edelim
                 {
                    await _publishEndpoint.Publish(new CartUpdatedEvent
                    {
                        ProductId = request.CreateCartItem.ProductId,
                        Quantity = cartItem.Quantity, // Güncel miktar
                        CartId = cart.Id,
                        UserId = userId,
                        CartItemId = cartItem.Id
                    }, cancellationToken);
                    _logger.LogInformation("CartUpdatedEvent published for CartItem {CartItemId}, Cart {CartId}, User {UserId}.", cartItem.Id, cart.Id, userId);
                 } else {
                     _logger.LogWarning("Cart was null when trying to publish CartUpdatedEvent for user {UserId}.", userId);
                 }


                return new CreatedCartResponse(); // Başarılı response
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error adding item (Product: {ProductId}) to cart for user {UserId}.", request.CreateCartItem.ProductId, userId);
                 throw; // Hata tekrar fırlatılır
            }
        }
    }
}