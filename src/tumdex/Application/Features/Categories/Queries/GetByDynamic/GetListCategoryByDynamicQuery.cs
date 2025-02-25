using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Categories.Dtos;
using Application.Features.Categories.Rules;
using Application.Features.Products.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetByDynamic;

public class GetListCategoryByDynamicQuery : IRequest<GetListResponse<GetListCategoryByDynamicDto>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }

    public string CacheKey => $"GetListCategoryByDynamicQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
    public bool BypassCache { get; } = true;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);

    public class GetListByDynamicCategoryQueryHandler : IRequestHandler<GetListCategoryByDynamicQuery,
        GetListResponse<GetListCategoryByDynamicDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IStorageService _storageService;

        public GetListByDynamicCategoryQueryHandler(ICategoryRepository categoryRepository, IMapper mapper,
            CategoryBusinessRules categoryBusinessRules, IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _categoryBusinessRules = categoryBusinessRules;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetListCategoryByDynamicDto>> Handle(GetListCategoryByDynamicQuery request,
            CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                var allCategories = await _categoryRepository.GetAllByDynamicAsync(
                    request.DynamicQuery,
                    include: q => q
                        .Include(c => c.Products)
                            .ThenInclude(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                        .Include(c => c.SubCategories)
                            .ThenInclude(sc => sc.Products)
                        .Include(c => c.CategoryImageFiles),
                    cancellationToken: cancellationToken);

                var categoriesDtos = _mapper.Map<GetListResponse<GetListCategoryByDynamicDto>>(allCategories);

                await EnrichCategoryDtos(categoriesDtos.Items, allCategories);

                return new GetListResponse<GetListCategoryByDynamicDto>
                {
                    Items = categoriesDtos.Items,
                    Index = 0,
                    Size = categoriesDtos.Count,
                    Count = categoriesDtos.Count,
                    Pages = 1,
                    HasNext = false,
                    HasPrevious = false
                };
            }
            else
            {
                IPaginate<Category> categories = await _categoryRepository.GetListByDynamicAsync(
                    request.DynamicQuery,
                    include: q => q
                        .Include(c => c.Products)
                            .ThenInclude(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                        .Include(c => c.SubCategories)
                            .ThenInclude(sc => sc.Products)
                        .Include(c => c.CategoryImageFiles),
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken);

                var categoriesDtos = _mapper.Map<GetListResponse<GetListCategoryByDynamicDto>>(categories);

                await EnrichCategoryDtos(categoriesDtos.Items, categories.Items);

                return new GetListResponse<GetListCategoryByDynamicDto>
                {
                    Items = categoriesDtos.Items,
                    Index = categories.Index,
                    Size = categories.Size,
                    Count = categories.Count,
                    Pages = categories.Pages,
                    HasNext = categories.HasNext,
                    HasPrevious = categories.HasPrevious
                };
            }
        }

        private async Task<List<GetListCategoryByDynamicDto>> GetSubCategoriesRecursively(
            string parentId, 
            CancellationToken cancellationToken)
        {
            var subCategories = await _categoryRepository.GetListAsync(
                predicate: c => c.ParentCategoryId == parentId,
                include: q => q
                    .Include(c => c.Products)
                        .ThenInclude(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                    .Include(c => c.SubCategories)
                        .ThenInclude(sc => sc.Products)
                    .Include(c => c.CategoryImageFiles),
                cancellationToken: cancellationToken
            );

            var subCategoryDtos = _mapper.Map<List<GetListCategoryByDynamicDto>>(subCategories.Items);
            await EnrichCategoryDtos(subCategoryDtos, subCategories.Items);
            return subCategoryDtos;
        }

        private async Task EnrichCategoryDtos(
            IEnumerable<GetListCategoryByDynamicDto> categoryDtos, 
            IEnumerable<Category> categories)
        {
            foreach (var categoryDto in categoryDtos)
            {
                var category = categories.FirstOrDefault(c => c.Id == categoryDto.Id);
                if (category == null) continue;

                // Alt kategorileri getir
                categoryDto.SubCategories = await GetSubCategoriesRecursively(categoryDto.Id, default);

                // Ürün görsellerini dönüştür
                if (category.Products != null)
                {
                    foreach (var product in categoryDto.Products)
                    {
                        var originalProduct = category.Products.FirstOrDefault(p => p.Id == product.Id);
                        if (originalProduct?.ProductImageFiles != null)
                        {
                            var showcaseImage = originalProduct.ProductImageFiles.FirstOrDefault(pif => pif.Showcase);
                            if (showcaseImage != null)
                            {
                                product.ShowcaseImage = showcaseImage.ToDto(_storageService);
                            }
                        }
                    }
                }

                // Kategori görselini dönüştür
                var categoryImage = category.CategoryImageFiles?.FirstOrDefault();
                if (categoryImage != null)
                {
                    categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                }
            }
        }
    }
}