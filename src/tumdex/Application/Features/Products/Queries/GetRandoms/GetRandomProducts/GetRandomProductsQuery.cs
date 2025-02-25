using Application.Consts;
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

namespace Application.Features.Products.Queries.GetRandoms.GetRandomProducts;

public class GetRandomProductsQuery : IRequest<GetListResponse<GetRandomProductsQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 20;
    
    public string CacheKey => "GetRandomProductsQuery";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);

    public class GetRandomProductsQueryHandler : IRequestHandler<GetRandomProductsQuery, GetListResponse<GetRandomProductsQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetRandomProductsQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetRandomProductsQueryResponse>> Handle(
            GetRandomProductsQuery request,
            CancellationToken cancellationToken)
        {
            IPaginate<Product> products = await _productRepository.GetListAsync(
                include: x => x
                    .Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase))
                    .Include(x => x.ProductFeatureValues)
                    .ThenInclude(x => x.FeatureValue)
                    .ThenInclude(x => x.Feature),
                cancellationToken: cancellationToken);

            // Random seçim için ürünleri karıştır ve istenen sayıda al
            var randomProducts = products.Items
                .OrderBy(x => Guid.NewGuid())
                .Take(request.Count)
                .ToList();

            // Base mapping
            var response = _mapper.Map<GetListResponse<GetRandomProductsQueryResponse>>(randomProducts);

            // Her bir ürün için showcase image'ı dönüştür ve URL'ini set et
            foreach (var productResponse in response.Items)
            {
                var product = randomProducts.First(p => p.Id == productResponse.Id);
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