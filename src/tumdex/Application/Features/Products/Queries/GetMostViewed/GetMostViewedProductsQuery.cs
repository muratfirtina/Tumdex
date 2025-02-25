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

namespace Application.Features.Products.Queries.GetMostViewed;

public class GetMostViewedProductsQuery : IRequest<GetListResponse<GetMostViewedProductQueryResponse>>, ICachableRequest
{
    public int Count { get; set; } = 10;
    public string CacheKey => "MostViewedProducts";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(20);

    public class GetMostViewedProductsQueryHandler : IRequestHandler<GetMostViewedProductsQuery, GetListResponse<GetMostViewedProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetMostViewedProductsQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetMostViewedProductQueryResponse>> Handle(
            GetMostViewedProductsQuery request,
            CancellationToken cancellationToken)
        {
            IPaginate<Product> products = await _productRepository.GetListAsync(
                predicate: x => x.ProductViews.Count > 0,
                include: x => x
                    .Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase))
                    .Include(x => x.ProductFeatureValues)
                    .ThenInclude(x => x.FeatureValue)
                    .ThenInclude(x => x.Feature),
                cancellationToken: cancellationToken);

            var mostViewedProducts = products.Items
                .OrderByDescending(x => x.ProductViews.Count)
                .Take(request.Count)
                .AsQueryable()
                .AsNoTracking()
                .ToList();

            // Base mapping
            var response = _mapper.Map<GetListResponse<GetMostViewedProductQueryResponse>>(mostViewedProducts);

            // Her bir ürün için showcase image'ı dönüştür ve URL'ini set et
            foreach (var productResponse in response.Items)
            {
                var product = mostViewedProducts.First(p => p.Id == productResponse.Id);
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