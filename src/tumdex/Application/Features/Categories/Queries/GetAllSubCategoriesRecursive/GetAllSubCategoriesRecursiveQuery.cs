using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.Extensions.Logging;
using Application.Extensions.ImageFileExtensions;
using Domain.Entities;

namespace Application.Features.Categories.Queries.GetAllSubCategoriesRecursive;
public class GetAllSubCategoriesRecursiveQuery : IRequest<GetListResponse<GetAllSubCategoriesRecursiveResponse>>, ICachableRequest
{
    public string? ParentCategoryId { get; set; }
    public string CacheKey => $"CategoriesSubRecursive-{ParentCategoryId}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1);

    public class GetAllSubCategoriesRecursiveQueryHandler : IRequestHandler<GetAllSubCategoriesRecursiveQuery, GetListResponse<GetAllSubCategoriesRecursiveResponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetAllSubCategoriesRecursiveQueryHandler> _logger;

        public GetAllSubCategoriesRecursiveQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetAllSubCategoriesRecursiveQueryHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        // GetAllSubCategoriesRecursiveQuery.cs dosyasındaki Handler sınıfını düzeltin
public async Task<GetListResponse<GetAllSubCategoriesRecursiveResponse>> Handle(
    GetAllSubCategoriesRecursiveQuery request, 
    CancellationToken cancellationToken)
{
    _logger.LogInformation("Fetching all recursive subcategories for ParentCategoryId: {ParentCategoryId}", 
        request.ParentCategoryId);

    // Repository metodu kategorileri getiriyor
    var allSubCategories = await _categoryRepository.GetAllSubCategoriesRecursiveAsync(
        request.ParentCategoryId, cancellationToken);

    if (allSubCategories == null || !allSubCategories.Any())
    {
        _logger.LogInformation("No subcategories found for ParentCategoryId: {ParentCategoryId}", 
            request.ParentCategoryId);
        return new GetListResponse<GetAllSubCategoriesRecursiveResponse> 
            { Items = new List<GetAllSubCategoriesRecursiveResponse>() };
    }

    try
    {
        // List<Category> -> List<DTO> dönüşümü
        var categoryDtos = _mapper.Map<List<GetAllSubCategoriesRecursiveResponse>>(allSubCategories);
        
        // Eksik bilgileri manuel doldur
        foreach (var dto in categoryDtos)
        {
            var entity = allSubCategories.FirstOrDefault(c => c.Id == dto.Id);
            if (entity != null)
            {
                // Derinlik hesaplama (opsiyonel)
                dto.Depth = CalculateDepth(entity, allSubCategories);
                
                // Görsel URL oluşturma
                if (entity.CategoryImageFiles?.FirstOrDefault() != null)
                {
                    dto.CategoryImage = entity.CategoryImageFiles.FirstOrDefault()
                        .ToDto(_storageService);
                }
            }
        }

        return new GetListResponse<GetAllSubCategoriesRecursiveResponse>
        {
            Items = categoryDtos,
            Count = categoryDtos.Count
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error mapping categories to DTOs");
        throw; // Hata yükselt ki düzgün bir yanıt dönelim
    }
}

private int CalculateDepth(Category category, List<Category> allCategories)
{
    int depth = 0;
    var current = category;
    
    while (current?.ParentCategoryId != null)
    {
        depth++;
        current = allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId);
    }
    
    return depth;
}
    }
}