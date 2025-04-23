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
namespace Application.Features.Categories.Queries.GetCategoriesByIds;
public class GetCategoriesByIdsQuery : IRequest<GetListResponse<GetCategoriesByIdsQueryResponse>>, ICachableRequest
{
    public List<string> Ids { get; set; }
    public string CacheKey => $"Categories-ByIds-{string.Join("_", Ids?.OrderBy(id => id) ?? Enumerable.Empty<string>())}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1);

    // --- Handler ---
    public class GetCategoriesByIdsQueryHandler : IRequestHandler<GetCategoriesByIdsQuery, GetListResponse<GetCategoriesByIdsQueryResponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetCategoriesByIdsQueryHandler> _logger;
        public GetCategoriesByIdsQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetCategoriesByIdsQueryHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<GetCategoriesByIdsQueryResponse>> Handle(GetCategoriesByIdsQuery request, CancellationToken cancellationToken)
        {
             if (request.Ids == null || !request.Ids.Any())
             {
                 _logger.LogWarning("GetCategoriesByIdsQuery called with no IDs.");
                 return new GetListResponse<GetCategoriesByIdsQueryResponse> { Items = new List<GetCategoriesByIdsQueryResponse>() };
             }

             _logger.LogInformation("Fetching categories for IDs: {CategoryIds}", string.Join(", ", request.Ids));

            List<Category> categories = await _categoryRepository.GetAllAsync(
                index: -1,
                size: -1,
                predicate: x => request.Ids.Contains(x.Id),
                include: c => c.Include(c => c.ParentCategory)
                .Include(c => c.CategoryImageFiles)
                .Include(c => c.SubCategories)
                .Include(c => c.Features)
                .ThenInclude(f => f.FeatureValues)
                .Include(fv => fv.Products),
                cancellationToken: cancellationToken
            );

             if (categories == null || !categories.Any())
             {
                 _logger.LogWarning("No categories found for the provided IDs: {CategoryIds}", string.Join(", ", request.Ids));
             }


             // List<Category> -> List<DTO>
             var categoryDtos = _mapper.Map<List<GetCategoriesByIdsQueryResponse>>(categories);

             // Resim ve ek bilgileri ayarla
             foreach (var categoryDto in categoryDtos)
             {
                 var categoryEntity = categories.FirstOrDefault(c => c.Id == categoryDto.Id);
                 if (categoryEntity != null)
                 {
                     var categoryImage = categoryEntity.CategoryImageFiles?.FirstOrDefault();
                     if (categoryImage != null)
                     {
                         categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                     }
                 }
             }


            // GetListResponse oluştur
            var response = new GetListResponse<GetCategoriesByIdsQueryResponse>
            {
                Items = categoryDtos,
                Count = categoryDtos.Count
            };

            _logger.LogInformation("Returning {Count} categories for IDs: {CategoryIds}", response.Count, string.Join(", ", request.Ids));
            return response;
        }
    }
}