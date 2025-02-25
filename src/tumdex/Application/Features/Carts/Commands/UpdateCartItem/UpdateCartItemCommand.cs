using Application.Events.OrderEvetns;
using Application.Repositories;
using Application.Services;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Carts.Commands.UpdateCartItem;

public class UpdateCartItemCommand : IRequest<UpdateCartItemResponse>, ICacheRemoverRequest
{
    public string CartItemId { get; set; }
    public bool IsChecked { get; set; } = true;
    
    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carts";
    
    
    public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, UpdateCartItemResponse>
    {
        private readonly ICartService _cartService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly ICurrentUserService _currentUserService;

        public UpdateCartItemCommandHandler(
            ICartService cartService,
            IPublishEndpoint publishEndpoint,
            ICartItemRepository cartItemRepository,
            ICurrentUserService currentUserService)
        {
            _cartService = cartService;
            _publishEndpoint = publishEndpoint;
            _cartItemRepository = cartItemRepository;
            _currentUserService = currentUserService;
        }

        public async Task<UpdateCartItemResponse> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
        {
            await _cartService.UpdateCartItemAsync(new()
            {
                CartItemId = request.CartItemId,
                IsChecked = request.IsChecked
            });

            var cartItem = await _cartItemRepository.GetAsync(
                predicate: ci => ci.Id == request.CartItemId,
                include: i => i.Include(ci => ci.Cart));

            var userId = await _currentUserService.GetCurrentUserIdAsync();

            await _publishEndpoint.Publish(new CartUpdatedEvent
            {
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                CartId = cartItem.CartId,
                UserId = userId
            }, cancellationToken);

            return new();
        }
    }
}