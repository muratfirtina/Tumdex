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

namespace Application.Features.Categories.Queries.GetSubCategoriesByBrandId;
public class GetSubCategoriesByBrandIdQuery : IRequest<GetListResponse<GetSubCategoriesByBrandIdQueryReponse>>, ICachableRequest // Yazım hatası düzeltildi
{
    public string BrandId { get; set; }

    
    public string CacheKey => $"CategoriesSub-ByBrand-{BrandId}"; 
    public bool BypassCache => false; 
    public string? CacheGroupKey => CacheGroups.Categories; 
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);

    // --- Handler ---
    public class GetSubCategoriesByBrandIdQueryHandler : IRequestHandler<GetSubCategoriesByBrandIdQuery, GetListResponse<GetSubCategoriesByBrandIdQueryReponse>> // Yazım hatası düzeltildi
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetSubCategoriesByBrandIdQueryHandler> _logger;

        public GetSubCategoriesByBrandIdQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetSubCategoriesByBrandIdQueryHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<GetSubCategoriesByBrandIdQueryReponse>> Handle(GetSubCategoriesByBrandIdQuery request, CancellationToken cancellationToken) // Yazım hatası düzeltildi
        {
            _logger.LogInformation("Fetching subcategories for BrandId: {BrandId}", request.BrandId);
            List<Category> categories = await _categoryRepository.GetAllAsync(
                index: -1,
                size: -1,
                predicate: x => x.Products.Any(p => p.BrandId == request.BrandId) && x.ParentCategoryId != null,
                include: c => c.Include(c => c.CategoryImageFiles),
                cancellationToken: cancellationToken
            );

             if (categories == null || !categories.Any())
             {
                 _logger.LogInformation("No subcategories found for BrandId: {BrandId}", request.BrandId);
                 return new GetListResponse<GetSubCategoriesByBrandIdQueryReponse> { Items = new List<GetSubCategoriesByBrandIdQueryReponse>() };
             }

            // List<Category> -> List<DTO>
            var categoryDtos = _mapper.Map<List<GetSubCategoriesByBrandIdQueryReponse>>(categories);

            // Resim URL'lerini ayarla
             foreach (var categoryDto in categoryDtos)
             {
                 var categoryEntity = categories.FirstOrDefault(c => c.Id == categoryDto.Id);
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
            var response = new GetListResponse<GetSubCategoriesByBrandIdQueryReponse>
            {
                Items = categoryDtos,
                Count = categoryDtos.Count
            };

            _logger.LogInformation("Returning {Count} subcategories for BrandId: {BrandId}", response.Count, request.BrandId);
            return response;
        }
    }
}