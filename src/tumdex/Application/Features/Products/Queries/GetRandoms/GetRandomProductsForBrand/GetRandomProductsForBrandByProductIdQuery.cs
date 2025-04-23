using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Core.CrossCuttingConcerns.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetRandoms.GetRandomProductsForBrand;

public class GetRandomProductsForBrandByProductIdQuery : IRequest<GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>, ICachableRequest
{
    public string ProductId { get; set; }
    public int Count { get; set; } = 10; // Varsayılan değer

    // ICachableRequest implementation
    public string CacheKey => $"RandomProducts-RelatedBrandToProduct-{ProductId}-Count{Count}"; // İlişkili marka ürünleri için key
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30); // 30 dk cache

    // --- Handler ---
    public class GetRandomProductsForBrandByProductIdQueryHandler : IRequestHandler<GetRandomProductsForBrandByProductIdQuery, GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetRandomProductsForBrandByProductIdQueryHandler> _logger; // Logger eklendi

        public GetRandomProductsForBrandByProductIdQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
            ILogger<GetRandomProductsForBrandByProductIdQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>> Handle(GetRandomProductsForBrandByProductIdQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching {Count} random products related to ProductId: {ProductId} (same brand).", request.Count, request.ProductId);

            // Referans ürünün markasını bul
            var referenceProduct = await _productRepository.GetAsync(
                predicate: x => x.Id == request.ProductId,
                cancellationToken: cancellationToken);

            if (referenceProduct == null)
            {
                _logger.LogWarning("Reference product not found with ID: {ProductId}", request.ProductId);
                throw new BusinessException($"Product with ID '{request.ProductId}' not found.");
            }
            var brandId = referenceProduct.BrandId;
             _logger.LogDebug("Reference product brand ID: {BrandId}", brandId);

             // Aynı markadaki diğer ürünlerin ID'lerini al
             var productIdsInBrand = await _productRepository.Query()
                 .Where(p => p.BrandId == brandId && p.Id != request.ProductId) // Aynı marka, farklı ürün
                 .Select(p => p.Id)
                 .ToListAsync(cancellationToken);

             if (!productIdsInBrand.Any())
             {
                  _logger.LogInformation("No other products found in the same brand ({BrandId}) as product {ProductId}.", brandId, request.ProductId);
                  return new GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse> { Items = new List<GetRandomProductsForBrandByProductIdQueryResponse>() };
             }


            // Rastgele ID seç
            var randomProductIds = productIdsInBrand
                .OrderBy(x => Guid.NewGuid())
                .Take(request.Count)
                .ToList();
             _logger.LogDebug("Selected {Count} random product IDs from the same brand.", randomProductIds.Count);

            // Seçilen ürünlerin detaylarını getir
            var randomProducts = await _productRepository.GetAllAsync(
                predicate: p => randomProductIds.Contains(p.Id),
                include: x => x.Include(p => p.Brand) // DTO'da varsa
                              .Include(p => p.Category) // DTO'da varsa
                              .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase)), // Resim için
                cancellationToken: cancellationToken);

            // List<Product> -> List<DTO>
            var productDtos = _mapper.Map<List<GetRandomProductsForBrandByProductIdQueryResponse>>(randomProducts);

             // Resim ayarla
             foreach (var productDto in productDtos)
             {
                 var productEntity = randomProducts.FirstOrDefault(p => p.Id == productDto.Id);
                 if (productEntity?.ProductImageFiles != null)
                 {
                     var showcaseImage = productEntity.ProductImageFiles.FirstOrDefault(); // Showcase filtrelendi
                     if (showcaseImage != null) productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                 }
             }


            // GetListResponse oluştur
            var response = new GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
            };

            _logger.LogInformation("Returning {Count} random related brand products for ProductId: {ProductId}", response.Count, request.ProductId);
            return response;
        }
    }
}