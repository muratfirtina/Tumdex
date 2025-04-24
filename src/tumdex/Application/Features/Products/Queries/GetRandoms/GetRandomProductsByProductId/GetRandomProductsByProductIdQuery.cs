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

namespace Application.Features.Products.Queries.GetRandoms.GetRandomProductsByProductId;

public class GetRandomProductsByProductIdQuery : IRequest<GetListResponse<GetRandomProductsByProductIdQueryResponse>>, ICachableRequest
{
    public string ProductId { get; set; }
    public int Count { get; set; } = 10; // Varsayılan değer

    // ICachableRequest implementation
    public string CacheKey => $"RandomProducts-RelatedToProduct-{ProductId}-Count{Count}"; // İlişkili ürünler için key
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30); // 30 dk cache

    // --- Handler ---
    public class GetRandomProductsByProductIdQueryHandler : IRequestHandler<GetRandomProductsByProductIdQuery, GetListResponse<GetRandomProductsByProductIdQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetRandomProductsByProductIdQueryHandler> _logger; // Logger eklendi

        public GetRandomProductsByProductIdQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
            ILogger<GetRandomProductsByProductIdQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetRandomProductsByProductIdQueryResponse>> Handle(GetRandomProductsByProductIdQuery request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Fetching {Count} random products related to ProductId: {ProductId} (same category).", request.Count, request.ProductId);

            // Referans ürünün kategorisini bul
            var referenceProduct = await _productRepository.GetAsync(
                predicate: x => x.Id == request.ProductId,
                cancellationToken: cancellationToken);

            if (referenceProduct == null)
            {
                _logger.LogWarning("Reference product not found with ID: {ProductId}", request.ProductId);
                throw new BusinessException($"Product with ID '{request.ProductId}' not found.");
            }
            var categoryId = referenceProduct.CategoryId;
             _logger.LogDebug("Reference product category ID: {CategoryId}", categoryId);

            // Aynı kategorideki diğer ürünlerin ID'lerini al
             var productIdsInCategory = await _productRepository.Query()
                 .Where(p => p.CategoryId == categoryId && p.Id != request.ProductId) // Aynı kategori, farklı ürün
                 .Select(p => p.Id)
                 .ToListAsync(cancellationToken);

             if (!productIdsInCategory.Any())
             {
                  _logger.LogInformation("No other products found in the same category ({CategoryId}) as product {ProductId}.", categoryId, request.ProductId);
                  return new GetListResponse<GetRandomProductsByProductIdQueryResponse> { Items = new List<GetRandomProductsByProductIdQueryResponse>() };
             }

            // Rastgele ID seç
            var randomProductIds = productIdsInCategory
                .OrderBy(x => Guid.NewGuid())
                .Take(request.Count)
                .ToList();
             _logger.LogDebug("Selected {Count} random product IDs from the same category.", randomProductIds.Count);

            // Seçilen ürünlerin detaylarını getir
            var randomProducts = await _productRepository.GetAllAsync(
                predicate: p => randomProductIds.Contains(p.Id),
                include: x => x.Include(p => p.Brand) // DTO'da varsa
                              .Include(p => p.Category) // DTO'da varsa
                              .Include(p => p.ProductFeatureValues)
                              .ThenInclude(pfv => pfv.FeatureValue)
                              .ThenInclude(fv => fv.Feature)
                              .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true)), // Resim için
                cancellationToken: cancellationToken);

            // List<Product> -> List<DTO>
            var productDtos = _mapper.Map<List<GetRandomProductsByProductIdQueryResponse>>(randomProducts);

             // Resim ayarla
             foreach (var productDto in productDtos)
             {
                 var productEntity = randomProducts.FirstOrDefault(p => p.Id == productDto.Id);
                 if (productEntity?.ProductImageFiles != null)
                 {
                     var showcaseImage = productEntity.ProductImageFiles.FirstOrDefault(pif=>pif.Showcase == true); // Showcase filtrelendi
                     if (showcaseImage != null) productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                 }
             }

            // GetListResponse oluştur
            var response = new GetListResponse<GetRandomProductsByProductIdQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
            };

            _logger.LogInformation("Returning {Count} random related products for ProductId: {ProductId}", response.Count, request.ProductId);
            return response;
        }
    }
}