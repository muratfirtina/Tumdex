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

namespace Application.Features.Products.Queries.GetRandoms.GetRandomProducts;

public class GetRandomProductsQuery : IRequest<GetListResponse<GetRandomProductsQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 20; // Varsayılan değer

    // ICachableRequest implementation
    public string CacheKey => $"RandomProducts-Count{Count}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30); // Rastgele liste 30 dk cache

    // --- Handler ---
    public class GetRandomProductsQueryHandler : IRequestHandler<GetRandomProductsQuery, GetListResponse<GetRandomProductsQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetRandomProductsQueryHandler> _logger; // Logger eklendi

        public GetRandomProductsQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
            ILogger<GetRandomProductsQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetRandomProductsQueryResponse>> Handle(
            GetRandomProductsQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching {Count} random products.", request.Count);

            // Tüm ürün ID'lerini al (veya daha optimize bir yöntem)
            var allProductIds = await _productRepository.Query()
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            if (!allProductIds.Any())
            {
                 _logger.LogInformation("No products found in the database.");
                 return new GetListResponse<GetRandomProductsQueryResponse> { Items = new List<GetRandomProductsQueryResponse>() };
            }

            // Rastgele ID seç
            var randomProductIds = allProductIds
                .OrderBy(x => Guid.NewGuid())
                .Take(request.Count)
                .ToList();
             _logger.LogDebug("Selected {Count} random product IDs.", randomProductIds.Count);

            // Seçilen ürünlerin detaylarını getir
            var randomProducts = await _productRepository.GetAllAsync(
                predicate: p => randomProductIds.Contains(p.Id),
                include: x => x.Include(p => p.Brand) // DTO'da varsa
                              .Include(p => p.Category) // DTO'da varsa
                              .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase)), // Resim için
                cancellationToken: cancellationToken);

            // List<Product> -> List<DTO>
            var productDtos = _mapper.Map<List<GetRandomProductsQueryResponse>>(randomProducts);

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
            var response = new GetListResponse<GetRandomProductsQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
            };

            _logger.LogInformation("Returning {Count} random products.", response.Count);
            return response;
        }
    }
}