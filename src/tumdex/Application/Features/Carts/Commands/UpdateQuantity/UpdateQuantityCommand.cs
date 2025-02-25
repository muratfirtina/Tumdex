using Application.Events.OrderEvetns;
using Application.Repositories;
using Application.Services;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MassTransit;
using MediatR;

namespace Application.Features.Carts.Commands.UpdateQuantity;

public class UpdateQuantityCommand : IRequest<UpdateQuantityResponse>, ICacheRemoverRequest
{
    public string CartItemId { get; set; }
    public int Quantity { get; set; }

    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carts";
    
    public class UpdateQuantityCommandHandler : IRequestHandler<UpdateQuantityCommand, UpdateQuantityResponse>
    {
        private readonly ICartService _cartService;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IStockReservationService _stockReservationService;
        

        public UpdateQuantityCommandHandler(
            ICartService cartService,
            IPublishEndpoint publishEndpoint,
             ICartItemRepository cartItemRepository, IStockReservationService stockReservationService)
        {
            _cartService = cartService;
            _publishEndpoint = publishEndpoint;
            
            _cartItemRepository = cartItemRepository;
            _stockReservationService = stockReservationService;
        }

        public async Task<UpdateQuantityResponse> Handle(UpdateQuantityCommand request, CancellationToken cancellationToken)
        {
            await _cartService.UpdateQuantityAsync(new()
            {
                CartItemId = request.CartItemId,
                Quantity = request.Quantity,
            });

            var cartItem = await _cartItemRepository.GetAsync(ci => ci.Id == request.CartItemId);
    
            // Update stock reservation with new quantity
            await _stockReservationService.CreateReservationAsync(cartItem.ProductId, request.CartItemId, request.Quantity);

            var cartInfo = await _cartService.GetCartInfoAsync(request.CartItemId);

            await _publishEndpoint.Publish(new CartUpdatedEvent
            {
                ProductId = cartItem.ProductId,
                Quantity = request.Quantity,
                CartId = cartInfo.CartId,
                UserId = cartInfo.UserId,
                CartItemId = request.CartItemId 
            }, cancellationToken);

            return new();
        }
    }
}