using Application.Services;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.Carts.Commands.RemoveCartItem;

public class RemoveCartItemCommand : IRequest<RemoveCartItemResponse>,ICacheRemoverRequest
{
    public string CartItemId { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carts";
    
    public class RemoveCartItemRequestHandler : IRequestHandler<RemoveCartItemCommand, RemoveCartItemResponse>
    {
        private readonly ICartService _cartService;

        public RemoveCartItemRequestHandler(ICartService cartService)
        {
            _cartService = cartService;
        }

        public async Task<RemoveCartItemResponse> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
        {
            await _cartService.RemoveCartItemAsync(request.CartItemId);
            return new();
        }
    }
    
    
}