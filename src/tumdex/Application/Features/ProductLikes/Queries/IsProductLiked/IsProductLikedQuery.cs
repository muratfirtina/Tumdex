using Application.Consts;
using Application.Repositories;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.ProductLikes.Queries.IsProductLiked;

public class IsProductLikedQuery : IRequest<IsProductLikedQueryResponse>, ICachableRequest
{
    public string ProductId { get; set; }
    public string CacheKey => $"IsProductLiked-{ProductId}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10);
    
    public class IsProductLikedQueryHandler : IRequestHandler<IsProductLikedQuery, IsProductLikedQueryResponse>
    {
        private readonly IProductLikeRepository _productLikeRepository;

        public IsProductLikedQueryHandler(IProductLikeRepository productLikeRepository)
        {
            _productLikeRepository = productLikeRepository;
        }

        public async Task<IsProductLikedQueryResponse> Handle(IsProductLikedQuery request, CancellationToken cancellationToken)
        {
            var isLiked = await _productLikeRepository.IsProductLikedAsync(request.ProductId);
            return new IsProductLikedQueryResponse { IsLiked = isLiked };
        }
    }
}