using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Categories.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Categories.Queries.Search;

public class SearchCategoryQuery : IRequest<GetListResponse<CategoryDto>>, ICachableRequest
{
    public string? SearchTerm { get; set; }

    // More descriptive cache key with search term
    public string CacheKey => $"Categories-Search-{SearchTerm}";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);

    public class SearchCategoryQueryHandler : IRequestHandler<SearchCategoryQuery, GetListResponse<CategoryDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public SearchCategoryQueryHandler(
            ICategoryRepository categoryRepository, 
            IMapper mapper,
            IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<CategoryDto>> Handle(
            SearchCategoryQuery request, 
            CancellationToken cancellationToken)
        {
            var categories = await _categoryRepository.SearchByNameAsync(request.SearchTerm);
            var response = _mapper.Map<GetListResponse<CategoryDto>>(categories);

            // Her kategori için görsel dönüşümü yap
            foreach (var categoryDto in response.Items)
            {
                var category = categories.Items.FirstOrDefault(c => c.Id == categoryDto.Id);
                if (category?.CategoryImageFiles != null)
                {
                    var categoryImage = category.CategoryImageFiles.FirstOrDefault();
                    if (categoryImage != null)
                    {
                        categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                    }
                }

                // Alt kategoriler için de görsel dönüşümü yap
                if (category?.SubCategories != null)
                {
                    foreach (var subCategoryDto in categoryDto.SubCategories)
                    {
                        var subCategory = category.SubCategories.FirstOrDefault(sc => sc.Id == subCategoryDto.Id);
                        if (subCategory?.CategoryImageFiles != null)
                        {
                            var subCategoryImage = subCategory.CategoryImageFiles.FirstOrDefault();
                            if (subCategoryImage != null)
                            {
                                subCategoryDto.CategoryImage = subCategoryImage.ToDto(_storageService);
                            }
                        }
                    }
                }
            }

            return response;
        }
    }
}