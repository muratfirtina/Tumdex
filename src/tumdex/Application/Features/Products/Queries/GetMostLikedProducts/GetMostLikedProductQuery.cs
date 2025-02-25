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
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetMostLikedProducts;

public class GetMostLikedProductQuery : IRequest<GetListResponse<GetMostLikedProductQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 10;
    public string CacheKey => "MostLikedProducts";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(20);
    
    public class GetMostLikedProductQueryHandler : IRequestHandler<GetMostLikedProductQuery, GetListResponse<GetMostLikedProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetMostLikedProductQueryHandler(IProductRepository productRepository, IStorageService storageService, IMapper mapper)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetMostLikedProductQueryResponse>> Handle(
            GetMostLikedProductQuery request, 
            CancellationToken cancellationToken)
        {
            // GetAllAsync kullanarak tüm ürünleri çek ve sorguyu optimize et
            var products = await _productRepository.GetAllAsync(
                predicate: x => x.ProductLikes.Count > 0,
                include: x => x
                    .Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase))
                    .Include(x => x.ProductFeatureValues)
                    .ThenInclude(x => x.FeatureValue)
                    .ThenInclude(x => x.Feature),
                orderBy: x => (IOrderedQueryable<Product>)x.OrderByDescending(p => p.ProductLikes.Count).Take(request.Count), // Direkt sorguda sıralama
                cancellationToken: cancellationToken);
    
            // Mapper ile response'u oluştur
            var response = _mapper.Map<GetListResponse<GetMostLikedProductQueryResponse>>(products);

            // Showcase image'ları dönüştür
            foreach (var productResponse in response.Items)
            {
                var product = products.First(p => p.Id == productResponse.Id);
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
