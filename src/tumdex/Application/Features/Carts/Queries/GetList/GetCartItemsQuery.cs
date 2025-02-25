using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Services;
using Application.Storage;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Carts.Queries.GetList;

public class GetCartItemsQuery : IRequest<List<GetCartItemsQueryResponse>>,ICachableRequest
{
    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Carts";
    public TimeSpan? SlidingExpiration { get; } = TimeSpan.FromMinutes(30);
    public class GetCartItemsQueryHandler : IRequestHandler<GetCartItemsQuery, List<GetCartItemsQueryResponse>>
    {
        private readonly ICartService _cartService;
        private readonly IStorageService _storageService;

        public GetCartItemsQueryHandler(ICartService cartService, IStorageService storageService)
        {
            _cartService = cartService;
            _storageService = storageService;
        }

        public async Task<List<GetCartItemsQueryResponse>> Handle(GetCartItemsQuery request, CancellationToken cancellationToken)
        {
            var cartItems = await _cartService.GetCartItemsAsync();
           
            return cartItems.Select(ci => 
            {
                var response = new GetCartItemsQueryResponse
                {
                    CartItemId = ci.Id.ToString(),
                    ProductName = ci.Product.Name,
                    BrandName = ci.Product.Brand?.Name,
                    Title = ci.Product.Title,
                    ProductFeatureValues = ci.Product.ProductFeatureValues.Select(pfv => new ProductFeatureValueDto
                    {
                        FeatureName = pfv.FeatureValue.Feature.Name,
                        FeatureValueName = pfv.FeatureValue.Name
                    }).ToList(),
                    Quantity = ci.Quantity,
                    UnitPrice = ci.Product.Price,
                    TotalPrice = ci.Product.Price * ci.Quantity,
                    IsChecked = ci.IsChecked
                };

                var showcaseImage = ci.Product.ProductImageFiles?.FirstOrDefault(pif => pif.Showcase);
                if (showcaseImage != null)
                {
                    response.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }

                return response;
            }).ToList();
        }
    }
}
