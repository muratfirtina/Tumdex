using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Services;
using Application.Storage;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Carts.Queries.GetList;

// GetCartItemsQuery - Yeni yapıya uygun olarak düzenlenmiş
public class GetCartItemsQuery : IRequest<List<GetCartItemsQueryResponse>>, ICachableRequest
{
    // ICachableRequest implementation
    // Sabit bir CacheKey - SmartCacheKeyGenerator kullanıcı ID'sini ekleyecek
    public string CacheKey => "UserCartItems"; // Generator "UserCartItems-User(userId)" yapacak
    public bool BypassCache => false;
    // CacheGroupKey olarak kullanıcıya özel grup
    public string? CacheGroupKey => CacheGroups.UserCarts;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(15); // Sepet içeriği 15 dk cache'lenebilir.

    // --- Handler ---
    public class GetCartItemsQueryHandler : IRequestHandler<GetCartItemsQuery, List<GetCartItemsQueryResponse>>
    {
        private readonly ICartService _cartService;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetCartItemsQueryHandler> _logger;
        private readonly ICurrentUserService _currentUserService;

        public GetCartItemsQueryHandler(
            ICartService cartService,
            IStorageService storageService,
            ILogger<GetCartItemsQueryHandler> logger,
            ICurrentUserService currentUserService)
        {
            _cartService = cartService;
            _storageService = storageService;
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<List<GetCartItemsQueryResponse>> Handle(GetCartItemsQuery request, CancellationToken cancellationToken)
        {
             string userId = "unknown";
             try
             {
                 userId = await _currentUserService.GetCurrentUserIdAsync();
                 _logger.LogInformation("Fetching cart items for user ID: {UserId}", userId);
             }
             catch (Exception ex)
             {
                 _logger.LogWarning(ex, "Could not get current user ID for fetching cart items.");
                 return new List<GetCartItemsQueryResponse>();
             }

            var cartItems = await _cartService.GetCartItemsAsync(); // List<Domain.Entities.CartItem?> döner

            if (cartItems == null || !cartItems.Any())
            {
                _logger.LogInformation("No cart items found for user ID: {UserId}", userId);
                return new List<GetCartItemsQueryResponse>();
            }

            var responseList = cartItems
               .Where(ci => ci != null && ci.Product != null) // Null kontrolleri
               .Select(ci =>
               {
                    // Null olamaz (Where kontrolü sayesinde)
                   var response = new GetCartItemsQueryResponse
                   {
                       CartItemId = ci!.Id,
                       ProductName = ci.Product.Name,
                       BrandName = ci.Product.Brand?.Name,
                       Title = ci.Product.Title,
                       ProductFeatureValues = ci.Product.ProductFeatureValues?.Select(pfv => new ProductFeatureValueDto
                       {
                           FeatureName = pfv.FeatureValue?.Feature?.Name,
                           FeatureValueName = pfv.FeatureValue?.Name
                       }).ToList() ?? new List<ProductFeatureValueDto>(),
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

            _logger.LogInformation("Returning {Count} cart items for user ID: {UserId}", responseList.Count, userId);
            return responseList;
        }
    }
}