using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetBestSelling;

public class GetBestSellingProductsQuery : IRequest<GetListResponse<GetBestSellingProductsQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 10; // Varsayılan değer

    // ICachableRequest implementation
    public string CacheKey => $"BestSellingProducts-Count{Count}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi olduğu için Products grubu.
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1); // Çok satanlar 1 saat cache.

    // --- Handler ---
    public class GetBestSellingProductsQueryHandler : IRequestHandler<GetBestSellingProductsQuery, GetListResponse<GetBestSellingProductsQueryResponse>>
    {
        private readonly IProductRepository _productRepository; // GetBestSellingProducts metodu için
        private readonly IOrderItemRepository _orderItemRepository; // Satış sayısını almak için (alternatif)
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetBestSellingProductsQueryHandler> _logger; // Logger eklendi

        public GetBestSellingProductsQueryHandler(
            IProductRepository productRepository,
            IOrderItemRepository orderItemRepository, // Eklendi
            IStorageService storageService,
            IMapper mapper,
            ILogger<GetBestSellingProductsQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _orderItemRepository = orderItemRepository; // Atandı
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetBestSellingProductsQueryResponse>> Handle(
            GetBestSellingProductsQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching {Count} best selling products.", request.Count);

            // Repository'den çok satan ürünleri al (Product listesi döner)
            List<Product> bestSellingProducts = await _productRepository.GetBestSellingProducts(request.Count);
            // VEYA OrderItemRepository kullanarak ID ve sayıyı alıp sonra Product'ları çekmek daha performanslı olabilir.
            // var topProductSales = await _orderItemRepository.GetMostOrderedProductsAsync(request.Count);
            // var productIds = topProductSales.Select(t => t.ProductId).ToList();
            // List<Product> bestSellingProducts = await _productRepository.GetAllAsync(p => productIds.Contains(p.Id), include: ...);

             if (bestSellingProducts == null || !bestSellingProducts.Any())
             {
                 _logger.LogInformation("No best selling products found.");
                 return new GetListResponse<GetBestSellingProductsQueryResponse> { Items = new List<GetBestSellingProductsQueryResponse>() };
             }


            // List<Product> -> List<DTO>
            var productDtos = _mapper.Map<List<GetBestSellingProductsQueryResponse>>(bestSellingProducts);

             // Resim ve ek bilgileri (örn. satış sayısı) ayarla
             // Eğer OrderItemRepo kullanıldıysa: var salesDict = topProductSales.ToDictionary(t => t.ProductId, t => t.OrderCount);
             foreach (var productDto in productDtos)
             {
                 var productEntity = bestSellingProducts.FirstOrDefault(p => p.Id == productDto.Id);
                 if (productEntity != null)
                 {
                     // Resim
                     var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault(pif => pif.Showcase);
                     if (showcaseImage != null)
                     {
                         productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                     }
                     // Satış sayısı (eğer OrderItemRepo kullanıldıysa)
                     // if (salesDict.TryGetValue(productDto.Id, out int count)) productDto.TotalSoldCount = count;
                 }
             }


            // GetListResponse oluştur
            var response = new GetListResponse<GetBestSellingProductsQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
                 // Index, Size vb. bu sorgu için anlamsız.
            };

            _logger.LogInformation("Returning {Count} best selling products.", response.Count);
            return response;
        }
    }
}