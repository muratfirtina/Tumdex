using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Domain;
using MediatR;

namespace Application.Features.Products.Queries.GetBestSelling;

public class GetBestSellingProductsQuery : IRequest<GetListResponse<GetBestSellingProductsQueryResponse>>,ICachableRequest
{
    public int Count { get; set; } = 10;
    public string CacheKey => "BestSellingProducts";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromDays(1);

    public class GetBestSellingProductsQueryHandler : IRequestHandler<GetBestSellingProductsQuery, GetListResponse<GetBestSellingProductsQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetBestSellingProductsQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetBestSellingProductsQueryResponse>> Handle(
            GetBestSellingProductsQuery request,
            CancellationToken cancellationToken)
        {
            List<Product> bestSellingProducts = await _productRepository.GetBestSellingProducts(request.Count);

            // Base mapping
            var response = _mapper.Map<GetListResponse<GetBestSellingProductsQueryResponse>>(bestSellingProducts);

            // Her bir ürün için showcase image'ı dönüştür ve URL'ini set et
            foreach (var productResponse in response.Items)
            {
                var product = bestSellingProducts.First(p => p.Id == productResponse.Id);
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