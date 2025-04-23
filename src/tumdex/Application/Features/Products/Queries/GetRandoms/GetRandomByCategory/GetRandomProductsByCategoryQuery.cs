using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Products.Queries.GetList;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetRandoms.GetRandomByCategory;

public class GetRandomProductsByCategoryQuery : IRequest<GetListResponse<GetAllProductQueryResponse>>, ICachableRequest
{
    public string CategoryId { get; set; }
    public int Count { get; set; } = 10; // Varsayılan değer

    // ICachableRequest implementation
    public string CacheKey => $"RandomProducts-Category{CategoryId}-Count{Count}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30); // Rastgele liste 30 dk cache

    // --- Handler ---
    public class GetRandomProductsByCategoryQueryHandler : IRequestHandler<GetRandomProductsByCategoryQuery, GetListResponse<GetAllProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository; // Alt kategorileri bulmak için
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetRandomProductsByCategoryQueryHandler> _logger; // Logger eklendi

        public GetRandomProductsByCategoryQueryHandler(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository, // Eklendi
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetRandomProductsByCategoryQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository; // Atandı
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetAllProductQueryResponse>> Handle(GetRandomProductsByCategoryQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching {Count} random products for CategoryId: {CategoryId} (including subcategories).", request.Count, request.CategoryId);

            // Ana kategori ve tüm alt kategorilerin ID'lerini al
            var categoryIds = await GetAllSubCategoryIdsRecursive(request.CategoryId, cancellationToken);
            _logger.LogDebug("Found {SubCategoryCount} subcategories including the parent for CategoryId: {CategoryId}", categoryIds.Count, request.CategoryId);

            // Bu kategorilerdeki ürünleri getir (rastgele sıralama için DB'den rastgele almak daha verimli olabilir ama EF Core'da direkt destek yok)
            // Önce ID'leri çekip sonra rastgele seçmek daha iyi olabilir.
            var productIdsInCategory = await _productRepository.Query()
                 .Where(p => categoryIds.Contains(p.CategoryId))
                 .Select(p => p.Id)
                 .ToListAsync(cancellationToken);

            if (!productIdsInCategory.Any())
            {
                 _logger.LogInformation("No products found in category {CategoryId} or its subcategories.", request.CategoryId);
                 return new GetListResponse<GetAllProductQueryResponse> { Items = new List<GetAllProductQueryResponse>() };
            }

            // Rastgele ID seç
            var randomProductIds = productIdsInCategory
                .OrderBy(x => Guid.NewGuid())
                .Take(request.Count)
                .ToList();
             _logger.LogDebug("Selected {Count} random product IDs.", randomProductIds.Count);

            // Seçilen ürünlerin detaylarını getir
            var randomProducts = await _productRepository.GetAllAsync(
                predicate: p => randomProductIds.Contains(p.Id),
                include: p => p.Include(x => x.Category) // DTO için gerekli
                              .Include(x => x.Brand)    // DTO için gerekli
                              .Include(p => p.ProductLikes) // DTO için gerekli
                              .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase)), // DTO için gerekli
                cancellationToken: cancellationToken);

             // List<Product> -> List<DTO> (GetAllProductQueryResponse kullanılıyor)
            var productDtos = _mapper.Map<List<GetAllProductQueryResponse>>(randomProducts);

            // Resim ve ek bilgi
             foreach (var productDto in productDtos)
             {
                 var productEntity = randomProducts.FirstOrDefault(p => p.Id == productDto.Id);
                 if (productEntity != null)
                 {
                     var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault(); // Showcase filtrelendi
                     if (showcaseImage != null) productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                     productDto.LikeCount = productEntity.ProductLikes?.Count ?? 0;
                 }
             }


            // GetListResponse oluştur
            var response = new GetListResponse<GetAllProductQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
            };

            _logger.LogInformation("Returning {Count} random products for category {CategoryId}.", response.Count, request.CategoryId);
            return response;
        }

        // Yardımcı metod: Bir kategorinin tüm alt kategorilerini getirir (recursive)
        private async Task<HashSet<string>> GetAllSubCategoryIdsRecursive(string categoryId, CancellationToken cancellationToken)
        {
            var ids = new HashSet<string> { categoryId };
            var subCategories = await _categoryRepository.GetAllAsync(
                predicate: c => c.ParentCategoryId == categoryId,
                cancellationToken: cancellationToken);

            foreach (var subCategory in subCategories)
            {
                if (ids.Add(subCategory.Id)) // Döngüye girmemek için kontrol
                {
                    var nestedIds = await GetAllSubCategoryIdsRecursive(subCategory.Id, cancellationToken);
                    ids.UnionWith(nestedIds);
                }
            }
            return ids;
        }
    }
}