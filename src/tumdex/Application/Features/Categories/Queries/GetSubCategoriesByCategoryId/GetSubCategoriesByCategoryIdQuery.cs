using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categories.Queries.GetSubCategoriesByCategoryId;
public class GetSubCategoriesByCategoryIdQuery : IRequest<GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>>, ICachableRequest // Yazım hatası düzeltildi
{
    public string ParentCategoryId { get; set; }
    public string CacheKey => $"CategoriesSub-ByParent-{ParentCategoryId}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1);

    // --- Handler ---
    public class GetSubCategoriesByCategoryIdQueryHandler : IRequestHandler<GetSubCategoriesByCategoryIdQuery, GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>> // Yazım hatası düzeltildi
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetSubCategoriesByCategoryIdQueryHandler> _logger; 

        public GetSubCategoriesByCategoryIdQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetSubCategoriesByCategoryIdQueryHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>> Handle(GetSubCategoriesByCategoryIdQuery request, CancellationToken cancellationToken) // Yazım hatası düzeltildi
        {
            _logger.LogInformation("Fetching subcategories for ParentCategoryId: {ParentCategoryId}", request.ParentCategoryId);

            List<Category> categories = await _categoryRepository.GetAllAsync(
                index: -1,
                size: -1,
                predicate: x => x.ParentCategoryId == request.ParentCategoryId,
                include: c => c.Include(c => c.ParentCategory)
                    .Include(c => c.CategoryImageFiles),
                cancellationToken: cancellationToken
            );

             if (categories == null || !categories.Any())
             {
                 _logger.LogInformation("No subcategories found for ParentCategoryId: {ParentCategoryId}", request.ParentCategoryId);
                 return new GetListResponse<GetSubCategoriesByCategoryIdQueryReponse> { Items = new List<GetSubCategoriesByCategoryIdQueryReponse>() };
             }


            // List<Category> -> List<DTO>
            var categoryDtos = _mapper.Map<List<GetSubCategoriesByCategoryIdQueryReponse>>(categories);

            // Resim ve ek bilgi (Parent Name)
            foreach (var categoryDto in categoryDtos)
            {
                var categoryEntity = categories.FirstOrDefault(c => c.Id == categoryDto.Id);
                if (categoryEntity != null)
                {
                    // Resim
                    var categoryImage = categoryEntity.CategoryImageFiles?.FirstOrDefault();
                    if (categoryImage != null)
                    {
                        categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                    }
                }
            }

            // GetListResponse oluştur
            var response = new GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>
            {
                Items = categoryDtos,
                Count = categoryDtos.Count
            };

            _logger.LogInformation("Returning {Count} subcategories for ParentCategoryId: {ParentCategoryId}", response.Count, request.ParentCategoryId);
            return response;
        }
    }
}