using Application.Consts;
using Application.Extensions;
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

namespace Application.Features.Products.Queries.GetMostLikedProducts;

public class GetMostLikedProductQuery : IRequest<GetListResponse<GetMostLikedProductQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 20;

    // ICachableRequest implementation
    public string CacheKey => $"MostLikedProducts-Count{Count}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün listesi
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1); // 1 saat cache

    // --- Handler ---
    public class GetMostLikedProductQueryHandler : IRequestHandler<GetMostLikedProductQuery, GetListResponse<GetMostLikedProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetMostLikedProductQueryHandler> _logger; // Logger eklendi

        public GetMostLikedProductQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
            ILogger<GetMostLikedProductQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetMostLikedProductQueryResponse>> Handle(
            GetMostLikedProductQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching {Count} most liked products.", request.Count);

            var products = await _productRepository.GetMostLikedProductsAsync(request.Count);

            if (products == null || !products.Any())
            {
                _logger.LogInformation("No liked products found.");
                return new GetListResponse<GetMostLikedProductQueryResponse> { Items = new List<GetMostLikedProductQueryResponse>() };
            }

            // List<Product> -> List<DTO>
            var productDtos = _mapper.Map<List<GetMostLikedProductQueryResponse>>(products);
            

            // Resim ve LikeCount ayarla
            foreach (var productDto in productDtos)
             {
                 var productEntity = products.FirstOrDefault(p => p.Id == productDto.Id);
                 if (productEntity != null)
                 {
                     // Resim
                     var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault(pif => pif.Showcase);
                     if (showcaseImage != null)
                     {
                         productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                         productDto.LikeCount = productEntity.ProductLikes?.Count ?? 0; // Zaten 0'dan büyük olacak (Where filtresi)
                     }
                     // Satış sayısı (eğer OrderItemRepo kullanıldıysa)
                     // if (salesDict.TryGetValue(productDto.Id, out int count)) productDto.TotalSoldCount = count;
                 }
             }

            // GetListResponse oluştur
            var response = new GetListResponse<GetMostLikedProductQueryResponse>
            {
                 Items = productDtos,
                 Count = productDtos.Count
            };

            _logger.LogInformation("Returning {Count} most liked products.", response.Count);
            return response;
        }
    }
}
