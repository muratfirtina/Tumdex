using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Products.Queries.GetList;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetRandoms.GetRandomByCategory;

public class GetRandomProductsByCategoryQuery : IRequest<GetListResponse<GetAllProductQueryResponse>>, ICachableRequest
{
    public string CategoryId { get; set; }
    public int Count { get; set; }
    public string CacheKey => "GetRandomProductsByCategoryQuery";
    public bool BypassCache => true;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
}

public class GetRandomProductsByCategoryQueryHandler : IRequestHandler<GetRandomProductsByCategoryQuery,
    GetListResponse<GetAllProductQueryResponse>>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IStorageService _storageService;
    private readonly IMapper _mapper;

    public GetRandomProductsByCategoryQueryHandler(IProductRepository productRepository,
        ICategoryRepository categoryRepository, IMapper mapper, IStorageService storageService)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _mapper = mapper;
        _storageService = storageService;
    }

    public async Task<GetListResponse<GetAllProductQueryResponse>> Handle(GetRandomProductsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        var categoryIds = await GetAllSubcategoryIds(request.CategoryId);

        var products = await _productRepository.GetListAsync(
            predicate: p => categoryIds.Contains(p.CategoryId),
            include: p => p.Include(x => x.Category)
                .Include(x => x.Brand)
                .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature)
                .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase)),
            cancellationToken: cancellationToken
        );

        var randomProducts = products.Items
            .OrderBy(x => products.Items.Count(y => y.Id == x.Id))
            .Take(request.Count)
            .ToList();

        var mappedProducts = _mapper.Map<GetListResponse<GetAllProductQueryResponse>>(randomProducts);

        // URL'leri oluştur
        foreach (var product in mappedProducts.Items)
        {
            var productEntity = randomProducts.First(p => p.Id == product.Id);
            var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault();
            if (showcaseImage != null)
            {
                product.ShowcaseImage = showcaseImage.ToDto(_storageService);
            }
        }

        return mappedProducts;
    }

    private async Task<HashSet<string>> GetAllSubcategoryIds(string categoryId)
    {
        var result = new HashSet<string> { categoryId };
        await AddSubcategoryIdsRecursively(categoryId, result);
        return result;
    }

    private async Task AddSubcategoryIdsRecursively(string categoryId, HashSet<string> categoryIds)
    {
        var subcategories = await _categoryRepository.GetListAsync(c => c.ParentCategoryId == categoryId);
        foreach (var subcategory in subcategories.Items)
        {
            if (categoryIds.Add(subcategory.Id)) // If the category wasn't already in the set
            {
                await AddSubcategoryIdsRecursively(subcategory.Id, categoryIds);
            }
        }
    }
}