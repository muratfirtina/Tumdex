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
            
            var products = await _productRepository.GetMostViewedProductsAsync(request.Count);

             if (products == null || !products.Any())
             {
                  _logger.LogInformation("No viewed products found.");
                  return new GetListResponse<GetMostViewedProductQueryResponse> { Items = new List<GetMostViewedProductQueryResponse>() };
             }

            // List<Product> -> List<DTO>
            var productDtos = _mapper.Map<List<GetMostViewedProductQueryResponse>>(products);

            // Resim ve ViewCount ayarla
             foreach (var productDto in productDtos)
             {
                 var productEntity = products.FirstOrDefault(p => p.Id == productDto.Id);
                 if (productEntity != null)
                 {
                     var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault(); // Showcase filtrelendi
                     if (showcaseImage != null) productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                     productDto.ViewCount = productEntity.ProductViews?.Count ?? 0; // Görüntülenme sayısı
                 }
             }


            // GetListResponse oluştur
            var response = new GetListResponse<GetMostViewedProductQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
            };

             _logger.LogInformation("Returning {Count} most viewed products.", response.Count);
            return response;
        }
    }
}