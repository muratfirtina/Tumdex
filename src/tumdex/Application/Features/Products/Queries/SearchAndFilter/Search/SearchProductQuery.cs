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
using MediatR;

namespace Application.Features.Products.Queries.SearchAndFilter.Search;

public class SearchProductQuery : IRequest<SearchResponse>, ICachableRequest
{
    public string SearchTerm { get; set; }
    public PageRequest PageRequest { get; set; }
    
    // More descriptive cache key with search term and pagination info
    public string CacheKey => $"Products-Search-{SearchTerm ?? "all"}-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);

    public class SearchProductQueryHandler : IRequestHandler<SearchProductQuery, SearchResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductLikeRepository _productLikeRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public SearchProductQueryHandler(IProductRepository productRepository, IStorageService storageService, IMapper mapper, IProductLikeRepository productLikeRepository)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _productLikeRepository = productLikeRepository;
        }

        public async Task<SearchResponse> Handle(SearchProductQuery request, CancellationToken cancellationToken)
        {
            var (productsPage, categories, brands) = await _productRepository.SearchProductsAsync(
                request.SearchTerm,
                request.PageRequest.PageIndex,
                request.PageRequest.PageSize);

            var productDtos = _mapper.Map<GetListResponse<SearchProductQueryResponse>>(productsPage);

            foreach (var productDto in productDtos.Items)
            {
                var product = productsPage.Items.First(p => p.Id == productDto.Id);
                var showcaseImage = product.ProductImageFiles?.FirstOrDefault(pif => pif.Showcase);
                if (showcaseImage != null)
                {
                    productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
            }

            var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);
            var brandDtos = _mapper.Map<List<BrandDto>>(brands);

            return new SearchResponse
            {
                Products = productDtos,
                Categories = categoryDtos,
                Brands = brandDtos
            };
        }
    }
}