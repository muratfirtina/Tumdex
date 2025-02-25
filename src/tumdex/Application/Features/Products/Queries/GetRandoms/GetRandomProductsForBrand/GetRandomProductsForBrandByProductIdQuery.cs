using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetRandoms.GetRandomProductsForBrand;

public class GetRandomProductsForBrandByProductIdQuery : IRequest<GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>, ICachableRequest
{
    public string ProductId { get; set; }

    public string CacheKey => "GetRandomProductsForBrandByProductIdQuery(" + ProductId + ")";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
    
    public class GetRandomProductsForBrandByProductIdQueryHandler : IRequestHandler<GetRandomProductsForBrandByProductIdQuery, GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetRandomProductsForBrandByProductIdQueryHandler(IProductRepository productRepository, IStorageService storageService, IMapper mapper)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>> Handle(GetRandomProductsForBrandByProductIdQuery request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetAsync(predicate: x => x.Id == request.ProductId, include: x => x.Include(x => x.Brand));
            var brandId = product.BrandId;
            
            var products = await _productRepository.GetListAsync(
                predicate: x => x.BrandId == brandId && x.Id != request.ProductId,
                include: x => x
                    .Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true))
                    .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature)
                );
            
            var randomProducts = products.Items
                .OrderBy(x => Guid.NewGuid())
                .Take(10)
                .ToList();
            
            GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse> response = _mapper.Map<GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>(randomProducts);
            
            foreach (var productDto in response.Items)
            {
                var productEntity = randomProducts.First(p => p.Id == productDto.Id);
                var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault();
                if (showcaseImage != null)
                {
                    productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
            }
            return response;

        }
    }
}
