using Application.Consts;
using Application.Repositories;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.ProductLikes.Commands.AddProductLike;

public class AddProductLikeCommand : IRequest<AddProductLikeResponse>, ICacheRemoverRequest
{
    public string ProductId { get; set; }

    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class AddProductLikeCommandHandler : IRequestHandler<AddProductLikeCommand, AddProductLikeResponse>
    {
        private readonly IProductLikeRepository _productLikeRepository;

        public AddProductLikeCommandHandler(IProductLikeRepository productLikeRepository)
        {
            _productLikeRepository = productLikeRepository;
        }

        public async Task<AddProductLikeResponse> Handle(AddProductLikeCommand request, CancellationToken cancellationToken)
        {
            var isLiked = await _productLikeRepository.IsProductLikedAsync(request.ProductId);

            if (isLiked)
            {
                var removed = await _productLikeRepository.RemoveProductLikeAsync(request.ProductId);
                return new AddProductLikeResponse { IsLiked = false };
            }
            else
            {
                await _productLikeRepository.AddProductLikeAsync(request.ProductId);
                return new AddProductLikeResponse { IsLiked = true };
            }
        }
    }
}