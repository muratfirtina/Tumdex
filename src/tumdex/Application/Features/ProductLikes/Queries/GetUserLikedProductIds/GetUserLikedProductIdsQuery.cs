using Application.Consts;
using Application.Repositories;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.ProductLikes.Queries.GetUserLikedProductIds;

public class GetUserLikedProductIdsQuery : IRequest<GetUserLikedProductIdsResponse>, ICachableRequest
{
    public string? SearchProductIds { get; set; }
    public string CacheKey => "UserLikedProductIds";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10);
    
    public class GetUserLikedProductIdsQueryHandler : IRequestHandler<GetUserLikedProductIdsQuery, GetUserLikedProductIdsResponse>
    {
        private readonly IProductLikeRepository _productLikeRepository;

        public GetUserLikedProductIdsQueryHandler(IProductLikeRepository productLikeRepository)
        {
            _productLikeRepository = productLikeRepository;
        }

        public async Task<GetUserLikedProductIdsResponse> Handle(GetUserLikedProductIdsQuery request, CancellationToken cancellationToken)
        {
            var likedProductIds = await _productLikeRepository.GetUserLikedProductIdsAsync(request.SearchProductIds);
            return new GetUserLikedProductIdsResponse { LikedProductIds = likedProductIds };
        }
    }
}