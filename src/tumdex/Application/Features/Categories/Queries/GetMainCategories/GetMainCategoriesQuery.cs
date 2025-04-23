using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categories.Queries.GetMainCategories;
public class GetMainCategoriesQuery : IRequest<GetListResponse<GetMainCategoriesResponse>>, ICachableRequest // İsim düzeltildi
{
    public PageRequest PageRequest { get; set; }
    public string CacheKey => $"CategoriesMain-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(4);

    // --- Handler ---
    public class GetMainCategoriesQueryHandler : IRequestHandler<GetMainCategoriesQuery, GetListResponse<GetMainCategoriesResponse>> // İsim düzeltildi
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetMainCategoriesQueryHandler> _logger;

        public GetMainCategoriesQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetMainCategoriesQueryHandler> logger) // Logger eklendi
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetMainCategoriesResponse>> Handle(GetMainCategoriesQuery request,
            CancellationToken cancellationToken) // İsim düzeltildi
        {
            _logger.LogInformation("Executing GetMainCategoriesQuery. PageIndex: {PageIndex}, PageSize: {PageSize}",
                request.PageRequest.PageIndex, request.PageRequest.PageSize);

            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                List<Category> categories = await _categoryRepository.GetAllAsync(
                    predicate: x => x.ParentCategoryId == null,
                    include: x => x
                        .Include(x => x.CategoryImageFiles)
                        .Include(x => x.Products)
                        .Include(x => x.SubCategories)
                        .ThenInclude(sc => sc.Products),
                    orderBy: x => x.OrderBy(c => c.Name),
                    cancellationToken: cancellationToken);

                // List<Category> -> List<DTO>
                var categoryDtos = _mapper.Map<List<GetMainCategoriesResponse>>(categories);

                // Resim ve ek bilgi
                foreach (var categoryDto in categoryDtos)
                {
                    var categoryEntity = categories.FirstOrDefault(c => c.Id == categoryDto.Id);
                    if (categoryEntity != null)
                    {
                        var categoryImage = categoryEntity.CategoryImageFiles?.FirstOrDefault();
                        if (categoryImage != null) categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                        categoryDto.ProductCount = categoryEntity.Products?.Count ?? 0;
                    }
                }

                // GetListResponse oluştur
                var response = new GetListResponse<GetMainCategoriesResponse>
                {
                    Items = categoryDtos,
                    Count = categoryDtos.Count,
                    Index = -1, Size = -1, Pages = categoryDtos.Any() ? 1 : 0, HasNext = false, HasPrevious = false
                };
                _logger.LogInformation("Returned {Count} total main categories.", response.Count);
                return response;

            }
            else
            {
                // Sayfalı getir
                _logger.LogDebug("Fetching paginated main categories. Page: {PageIndex}, Size: {PageSize}",
                    request.PageRequest.PageIndex, request.PageRequest.PageSize);
                IPaginate<Category> categoriesPaginated = await _categoryRepository.GetListAsync(
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    orderBy: x => x.OrderBy(c => c.Name),
                    predicate: x => x.ParentCategoryId == null,
                    include: x => x
                        .Include(x => x.CategoryImageFiles)
                        .Include(x => x.Products)
                        .Include(x => x.SubCategories)
                        .ThenInclude(sc => sc.Products),
                    cancellationToken: cancellationToken);

                // IPaginate -> GetListResponse
                var response = _mapper.Map<GetListResponse<GetMainCategoriesResponse>>(categoriesPaginated);

                // Resim ve ek bilgi
                if (response.Items != null && response.Items.Any()) // Null kontrolü
                {
                    foreach (var categoryDto in response.Items)
                    {
                        var categoryEntity = categoriesPaginated.Items.FirstOrDefault(c => c.Id == categoryDto.Id);
                        if (categoryEntity != null)
                        {
                            var categoryImage = categoryEntity.CategoryImageFiles?.FirstOrDefault();
                            if (categoryImage != null) categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                            categoryDto.ProductCount = categoryEntity.Products?.Count ?? 0;

                        }
                    }
                }

                _logger.LogInformation(
                    "Returned {Count} main categories on page {PageIndex}. Total items: {TotalCount}",
                    response.Items?.Count ?? 0, response.Index, response.Count);
                return response;
            }
        }
    }
}