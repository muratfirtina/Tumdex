using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Dtos;
using Application.Features.Categories.Dtos;
using Application.Features.ProductImageFiles.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.SearchAndFilter.Search;
public class SearchProductQuery : IRequest<SearchResponse>, ICachableRequest
{
    public string? SearchTerm { get; set; }
    public PageRequest PageRequest { get; set; }

    // ICachableRequest implementation
    public string CacheKey => $"Products-Search-{SearchTerm?.Trim().ToLower() ?? "all"}-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10); // Arama sonuçları 10 dk cache

    public class SearchProductQueryHandler : IRequestHandler<SearchProductQuery, SearchResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly IProductLikeRepository _productLikeRepository; // Kullanılmıyor, kaldırılabilir
        private readonly ILogger<SearchProductQueryHandler> _logger; // Logger eklendi

        public SearchProductQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
            IProductLikeRepository productLikeRepository, // Kaldırılabilir
            ILogger<SearchProductQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _productLikeRepository = productLikeRepository; // Kaldırılabilir
            _logger = logger; // Atandı
        }

        public async Task<SearchResponse> Handle(SearchProductQuery request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Executing SearchProductQuery. SearchTerm: '{SearchTerm}', Page: {Page}, Size: {Size}",
                 request.SearchTerm ?? "N/A", request.PageRequest.PageIndex, request.PageRequest.PageSize);

            // Repository'den arama sonuçlarını al (Ürünler, Kategoriler, Markalar döner)
            var (productsPage, categories, brands) = await _productRepository.SearchProductsAsync(
                request.SearchTerm,
                request.PageRequest.PageIndex,
                request.PageRequest.PageSize
                 // cancellationToken repository metoduna geçirilmiyorsa burada eklenmeli.
                );

             if (productsPage == null || productsPage.Items == null) // Null kontrolü
             {
                 _logger.LogWarning("SearchProductsAsync returned null or empty products.");
                 // Boş bir response döndür
                 return new SearchResponse {
                     Products = new GetListResponse<SearchProductQueryResponse>{ Items = new List<SearchProductQueryResponse>() },
                     Categories = new List<CategoryDto>(),
                     Brands = new List<BrandDto>()
                 };
             }


            // Ürünleri DTO'ya map'le
            var productDtos = _mapper.Map<GetListResponse<SearchProductQueryResponse>>(productsPage);

            // Ürün resimlerini ayarla
             if (productDtos.Items != null && productDtos.Items.Any()) // Null kontrolü
             {
                foreach (var productDto in productDtos.Items)
                {
                    var productEntity = productsPage.Items.FirstOrDefault(p => p.Id == productDto.Id);
                    if (productEntity?.ProductImageFiles != null)
                    {
                        var showcaseImage = productEntity.ProductImageFiles.FirstOrDefault(pif => pif.Showcase);
                        if (showcaseImage != null)
                        {
                            productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                        }
                    }
                }
             }

            // Kategori ve Markaları DTO'ya map'le
            var categoryDtos = _mapper.Map<List<CategoryDto>>(categories ?? new List<Category>());
            var brandDtos = _mapper.Map<List<BrandDto>>(brands ?? new List<Brand>());

            // Ana response'u oluştur
            var response = new SearchResponse
            {
                Products = productDtos,
                Categories = categoryDtos,
                Brands = brandDtos
            };

            _logger.LogInformation("Search completed. Found {ProductCount} products, {CategoryCount} categories, {BrandCount} brands.",
                response.Products.Count, response.Categories.Count, response.Brands.Count);

            return response;
        }
    }
}