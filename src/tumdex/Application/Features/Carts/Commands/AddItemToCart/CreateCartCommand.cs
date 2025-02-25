using Application.Events.OrderEvetns;
using Application.Features.Carts.Dtos;
using Application.Services;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MassTransit;
using MediatR;

namespace Application.Features.Carts.Commands.AddItemToCart;

public class CreateCartCommand : IRequest<CreatedCartResponse>, ICacheRemoverRequest
{
    public CreateCartCommand(CreateCartItemDto createCartItem)
    {
        CreateCartItem = createCartItem;
    }

    public CreateCartItemDto CreateCartItem { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carts";

    public class CreateCartCommandHandler : IRequestHandler<CreateCartCommand, CreatedCartResponse>
    {
        private readonly ICartService _cartService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IStockReservationService _stockReservationService;
        private readonly ICurrentUserService _currentUserService;

        public CreateCartCommandHandler(
            ICartService cartService,
            IPublishEndpoint publishEndpoint,
            IStockReservationService stockReservationService,
            ICurrentUserService currentUserService)
        {
            _cartService = cartService;
            _publishEndpoint = publishEndpoint;
            _stockReservationService = stockReservationService;
            _currentUserService = currentUserService;
        }

        public async Task<CreatedCartResponse> Handle(CreateCartCommand request, CancellationToken cancellationToken)
        {
            // Önce cart item'ı ekle
            await _cartService.AddItemToCartAsync(request.CreateCartItem);

            // Yeni eklenen cart item'ı al
            var userId = await _currentUserService.GetCurrentUserIdAsync();
            var cart = await _cartService.GetUserActiveCart();
            var cartItem = cart.CartItems.First(ci => ci.ProductId == request.CreateCartItem.ProductId);

            // Stok rezervasyonu oluştur
            await _stockReservationService.CreateReservationAsync(
                request.CreateCartItem.ProductId,
                cartItem.Id, // CartItem.Id kullan
                request.CreateCartItem.Quantity
            );

            await _publishEndpoint.Publish(new CartUpdatedEvent
            {
                ProductId = request.CreateCartItem.ProductId,
                Quantity = request.CreateCartItem.Quantity,
                CartId = cart.Id,
                UserId = userId,
                CartItemId = cartItem.Id 
            }, cancellationToken);

            return new();
        }
    }
}