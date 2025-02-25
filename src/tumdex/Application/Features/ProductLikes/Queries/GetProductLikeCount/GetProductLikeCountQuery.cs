using Application.Consts;
using Application.Repositories;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.ProductLikes.Queries.GetProductLikeCount;

public class GetProductLikeCountQuery : IRequest<GetProductLikeCountQueryResponse>,ICachableRequest
{
    public string ProductId { get; set; }
    
    public string CacheKey => $"ProductLikeCount-{ProductId}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10);
    
    public class GetProductLikeCountQueryHandler : IRequestHandler<GetProductLikeCountQuery, GetProductLikeCountQueryResponse>
    {
        private readonly IProductLikeRepository _productLikeRepository;

        public GetProductLikeCountQueryHandler(IProductLikeRepository productLikeRepository)
        {
            _productLikeRepository = productLikeRepository;
        }

        public async Task<GetProductLikeCountQueryResponse> Handle(GetProductLikeCountQuery request, CancellationToken cancellationToken)
        {
            var likeCount = await _productLikeRepository.GetProductLikeCountAsync(request.ProductId);
            return new GetProductLikeCountQueryResponse { LikeCount = likeCount };
        }
    }
}