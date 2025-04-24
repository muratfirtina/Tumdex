using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetMostViewed;

public class GetMostViewedProductsQuery : IRequest<GetListResponse<GetMostViewedProductQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 10;

    // ICachableRequest implementation
    public string CacheKey => $"MostViewedProducts-Count{Count}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1); // 1 saat cache

    // --- Handler ---
    public class GetMostViewedProductsQueryHandler : IRequestHandler<GetMostViewedProductsQuery, GetListResponse<GetMostViewedProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository; // ProductView ilişkisini include etmek için
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetMostViewedProductsQueryHandler> _logger; // Logger eklendi

        public GetMostViewedProductsQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
             ILogger<GetMostViewedProductsQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetMostViewedProductQueryResponse>> Handle(
            GetMostViewedProductsQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching {Count} most viewed products.", request.Count);
            
            IPaginate<Product> products = await _productRepository.GetListAsync(
                predicate: x => x.ProductViews.Count > 0,
                include: x => x
                    .Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase))
                    .Include(x => x.ProductFeatureValues)
                    .ThenInclude(x => x.FeatureValue)
                    .ThenInclude(x => x.Feature),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Fetching {Count} most viewed products.", request.Count);
            
            var mostViewedProducts = products.Items
                .OrderByDescending(x => x.ProductViews.Count)
                .Take(request.Count)
                .AsQueryable()
                .AsNoTracking()
                .ToList();
            var response = _mapper.Map<GetListResponse<GetMostViewedProductQueryResponse>>(mostViewedProducts);
            
            foreach (var productResponse in response.Items)
            {
                var product = mostViewedProducts.First(p => p.Id == productResponse.Id);
                var showcaseImage = product.ProductImageFiles.FirstOrDefault(pif => pif.Showcase);
                if (showcaseImage != null)
                {
                    productResponse.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
            }
            return response;
        }
    }
}