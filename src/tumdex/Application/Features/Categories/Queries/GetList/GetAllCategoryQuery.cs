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

namespace Application.Features.Categories.Queries.GetList;
public class GetAllCategoryQuery : IRequest<GetListResponse<GetAllCategoryQueryResponse>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    
    public string CacheKey => $"Categories-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(2);

    // --- Handler ---
    public class GetAllCategoryQueryHandler : IRequestHandler<GetAllCategoryQuery, GetListResponse<GetAllCategoryQueryResponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetAllCategoryQueryHandler> _logger;

        public GetAllCategoryQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetAllCategoryQueryHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<GetAllCategoryQueryResponse>> Handle(GetAllCategoryQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing GetAllCategoryQuery. PageIndex: {PageIndex}, PageSize: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
             GetListResponse<GetAllCategoryQueryResponse> response;

             if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
             {
                 // Tümünü getir
                 _logger.LogDebug("Fetching all categories.");
                 List<Category> categories = await _categoryRepository.GetAllAsync(
                     include:c => c
                         .Include(c => c.CategoryImageFiles)
                         .Include(c => c.Products),
                     orderBy: q => q.OrderBy(c => c.Name),
                     cancellationToken: cancellationToken);

                 // List<Category> -> List<DTO>
                 var categoryDtos = _mapper.Map<List<GetAllCategoryQueryResponse>>(categories);

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
                 response = new GetListResponse<GetAllCategoryQueryResponse>
                 {
                     Items = categoryDtos,
                     Count = categoryDtos.Count,
                     Index = -1, Size = -1, Pages = categoryDtos.Any() ? 1 : 0, HasNext = false, HasPrevious = false
                 };
                 _logger.LogInformation("Returned {Count} total categories.", response.Count);
             }
             else
             {
                 // Sayfalı getir
                  _logger.LogDebug("Fetching paginated categories. Page: {PageIndex}, Size: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
                 IPaginate<Category> categoriesPaginated = await _categoryRepository.GetListAsync(
                     index: request.PageRequest.PageIndex,
                     size: request.PageRequest.PageSize,
                     include:c => c
                         .Include(c => c.CategoryImageFiles)
                         .Include(c => c.Products),
                      orderBy: q => q.OrderBy(c => c.Name),
                     cancellationToken: cancellationToken
                 );

                 // IPaginate -> GetListResponse
                 response = _mapper.Map<GetListResponse<GetAllCategoryQueryResponse>>(categoriesPaginated);

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
                  _logger.LogInformation("Returned {Count} categories on page {PageIndex}. Total items: {TotalCount}", response.Items?.Count ?? 0, response.Index, response.Count);
             }

             return response;
        }
    }
}