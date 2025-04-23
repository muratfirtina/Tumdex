// Application/Features/Categories/Queries/Search/SearchCategoryQuery.cs
using Application.Consts;
using Application.Dtos.Image; // ImageDto için
using Application.Extensions.ImageFileExtensions; // ToDto için
using Application.Features.Categories.Dtos; // CategoryDto için
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses; // GetListResponse için
using Core.Persistence.Paging; // IPaginate için
using MediatR;
using Microsoft.Extensions.Logging; // Logger eklendi
using System.Linq;
using Domain.Entities; // FirstOrDefault

namespace Application.Features.Categories.Queries.Search;

public class SearchCategoryQuery : IRequest<GetListResponse<CategoryDto>>, ICachableRequest
{
    public string? SearchTerm { get; set; }
    public string CacheKey => $"Categories-Search-{SearchTerm?.Trim().ToLower() ?? "all"}"; // Arama terimine göre key
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories; // Kategori grubuna ait.
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(15); // Arama sonuçları 15 dk cache.

    // --- Handler ---
    public class SearchCategoryQueryHandler : IRequestHandler<SearchCategoryQuery, GetListResponse<CategoryDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<SearchCategoryQueryHandler> _logger; // Logger eklendi

        public SearchCategoryQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<SearchCategoryQueryHandler> logger) // Logger eklendi
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<CategoryDto>> Handle(SearchCategoryQuery request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Searching categories with term: '{SearchTerm}'", request.SearchTerm ?? "N/A");

             // Repository metodu IPaginate<Category> dönmeli
             IPaginate<Category> categoriesPaginated = await _categoryRepository.SearchByNameAsync(request.SearchTerm); // Sayfalama eklenebilir

              if (categoriesPaginated == null || categoriesPaginated.Items == null) // Null kontrolü
              {
                  _logger.LogWarning("SearchByNameAsync returned null or empty items for term: '{SearchTerm}'", request.SearchTerm ?? "N/A");
                  return new GetListResponse<CategoryDto> { Items = new List<CategoryDto>() }; // Boş response
              }

             // IPaginate -> GetListResponse
             var response = _mapper.Map<GetListResponse<CategoryDto>>(categoriesPaginated);

             // Resimleri ayarla (hem ana kategori hem de alt kategoriler için - DTO yapısına bağlı)
             if (response.Items != null && response.Items.Any()) // Null kontrolü
             {
                foreach (var categoryDto in response.Items)
                {
                    var categoryEntity = categoriesPaginated.Items.FirstOrDefault(c => c.Id == categoryDto.Id);
                    if (categoryEntity != null)
                    {
                        // Ana kategori resmi
                        var categoryImage = categoryEntity.CategoryImageFiles?.FirstOrDefault();
                        if (categoryImage != null)
                        {
                            categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                        }

                        // Alt kategori resimleri (eğer DTO'da varsa ve include edildiyse)
                        if (categoryDto.SubCategories != null && categoryEntity.SubCategories != null)
                        {
                            foreach (var subCategoryDto in categoryDto.SubCategories)
                            {
                                var subCategoryEntity = categoryEntity.SubCategories.FirstOrDefault(sc => sc.Id == subCategoryDto.Id);
                                if (subCategoryEntity?.CategoryImageFiles != null)
                                {
                                    var subCategoryImage = subCategoryEntity.CategoryImageFiles.FirstOrDefault();
                                    if (subCategoryImage != null)
                                    {
                                        subCategoryDto.CategoryImage = subCategoryImage.ToDto(_storageService);
                                    }
                                }
                            }
                        }
                    }
                }
             }

             _logger.LogInformation("Found {Count} categories matching term: '{SearchTerm}'", response.Count, request.SearchTerm ?? "N/A");
             return response;
        }
    }
}