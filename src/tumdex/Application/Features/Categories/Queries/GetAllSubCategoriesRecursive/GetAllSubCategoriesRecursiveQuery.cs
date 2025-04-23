using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.Extensions.Logging;
using Application.Extensions.ImageFileExtensions;

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

        public async Task<GetListResponse<GetAllSubCategoriesRecursiveResponse>> Handle(GetAllSubCategoriesRecursiveQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching all recursive subcategories for ParentCategoryId: {ParentCategoryId}", request.ParentCategoryId);

            // Repository metodu List<Category> veya benzeri bir koleksiyon dönmeli.
            var allSubCategories = await _categoryRepository.GetAllSubCategoriesRecursiveAsync(request.ParentCategoryId, cancellationToken);

            if (allSubCategories == null || !allSubCategories.Any())
            {
                 _logger.LogInformation("No subcategories found for ParentCategoryId: {ParentCategoryId}", request.ParentCategoryId);
                 return new GetListResponse<GetAllSubCategoriesRecursiveResponse> { Items = new List<GetAllSubCategoriesRecursiveResponse>() };
            }

             // List<Category> -> List<DTO> mapleme
            var categoryDtos = _mapper.Map<List<GetAllSubCategoriesRecursiveResponse>>(allSubCategories);
            
            // Resim URL'lerini ayarla
             foreach (var categoryDto in categoryDtos)
             {
                 var categoryEntity = allSubCategories.FirstOrDefault(c => c.Id == categoryDto.Id);
                 if (categoryEntity?.CategoryImageFiles != null)
                 {
                     var categoryImage = categoryEntity.CategoryImageFiles.FirstOrDefault();
                     if (categoryImage != null)
                     {
                         categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                     }
                 }
             }


            // GetListResponse oluştur
            var response = new GetListResponse<GetAllSubCategoriesRecursiveResponse>
            {
                 Items = categoryDtos,
                 Count = categoryDtos.Count
            };

            _logger.LogInformation("Returning {Count} recursive subcategories for ParentCategoryId: {ParentCategoryId}", response.Count, request.ParentCategoryId);
            return response;
        }
    }
}