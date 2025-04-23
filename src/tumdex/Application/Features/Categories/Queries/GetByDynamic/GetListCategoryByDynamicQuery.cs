using System.Text.Json;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Categories.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categories.Queries.GetByDynamic;
public class GetListCategoryByDynamicQuery : IRequest<GetListResponse<GetListCategoryByDynamicDto>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }

    // ICachableRequest implementation
    public string CacheKey => $"Categories-Dynamic-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}-{JsonSerializer.Serialize(DynamicQuery)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories; 
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetListByDynamicCategoryQueryHandler : IRequestHandler<GetListCategoryByDynamicQuery, GetListResponse<GetListCategoryByDynamicDto>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly ILogger<GetListByDynamicCategoryQueryHandler> _logger;

        public GetListByDynamicCategoryQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            CategoryBusinessRules categoryBusinessRules,
            ILogger<GetListByDynamicCategoryQueryHandler> logger) // Logger eklendi
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _categoryBusinessRules = categoryBusinessRules;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetListCategoryByDynamicDto>> Handle(GetListCategoryByDynamicQuery request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Executing GetListCategoryByDynamicQuery. PageIndex: {PageIndex}, PageSize: {PageSize}, DynamicQuery: {DynamicQueryJson}",
                 request.PageRequest.PageIndex, request.PageRequest.PageSize, JsonSerializer.Serialize(request.DynamicQuery));
            GetListResponse<GetListCategoryByDynamicDto> response;

            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                // Tüm sonuçları getir
                _logger.LogDebug("Fetching all categories matching dynamic query.");
                var allCategories = await _categoryRepository.GetAllByDynamicAsync(
                    request.DynamicQuery,
                     include: q => q
                        .Include(c => c.Products)
                            .ThenInclude(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                        .Include(c => c.SubCategories)
                            .ThenInclude(sc => sc.Products)
                        .Include(c => c.CategoryImageFiles),
                    cancellationToken: cancellationToken);

                 // List<Category> -> List<DTO> mapleme
                var categoryDtos = _mapper.Map<List<GetListCategoryByDynamicDto>>(allCategories);

                // Resim URL'lerini, ürün sayılarını ve alt kategorileri ayarla (recursive olabilir)
                await EnrichCategoryDtos(categoryDtos, allCategories, cancellationToken);

                 // GetListResponse oluştur
                response = new GetListResponse<GetListCategoryByDynamicDto>
                {
                    Items = categoryDtos,
                    Count = categoryDtos.Count,
                    Index = -1, Size = -1, Pages = categoryDtos.Any() ? 1 : 0, HasNext = false, HasPrevious = false
                };
                _logger.LogInformation("Returned {Count} total categories matching dynamic query.", response.Count);
            }
            else
            {
                // Sayfalama ile sonuçları getir
                 _logger.LogDebug("Fetching paginated categories matching dynamic query. Page: {PageIndex}, Size: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
                IPaginate<Category> categoriesPaginated = await _categoryRepository.GetListByDynamicAsync(
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

                 // IPaginate<Category> -> GetListResponse<DTO> mapleme
                 response = _mapper.Map<GetListResponse<GetListCategoryByDynamicDto>>(categoriesPaginated);

                 // Resim URL'lerini, ürün sayılarını ve alt kategorileri ayarla
                 if (response.Items != null && response.Items.Any()) // Null kontrolü
                 {
                     await EnrichCategoryDtos(response.Items, categoriesPaginated.Items, cancellationToken);
                 }

                 _logger.LogInformation("Returned {Count} categories on page {PageIndex} matching dynamic query. Total items: {TotalCount}", response.Items?.Count ?? 0, response.Index, response.Count);
            }

            return response;
        }

         // Yardımcı metod: DTO'ları zenginleştirir (resim, ürün sayısı, alt kategoriler)
        private async Task EnrichCategoryDtos(
            IEnumerable<GetListCategoryByDynamicDto> categoryDtos,
            IEnumerable<Category> categories,
            CancellationToken cancellationToken)
        {
            foreach (var categoryDto in categoryDtos)
            {
                var categoryEntity = categories.FirstOrDefault(c => c.Id == categoryDto.Id);
                if (categoryEntity == null) continue;

                // Kategori görseli
                var categoryImage = categoryEntity.CategoryImageFiles?.FirstOrDefault();
                if (categoryImage != null)
                {
                    categoryDto.CategoryImage = categoryImage.ToDto(_storageService);
                }

                // Alt kategoriler (recursive çağrı) - DTO'da SubCategories varsa
                if (categoryDto.SubCategories != null) // DTO'da SubCategories varsa doldurmaya çalış
                {
                    categoryDto.SubCategories = await GetSubCategoriesRecursively(categoryDto.Id, cancellationToken);
                }
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
             // Recursive olarak alt kategorileri de zenginleştir
            await EnrichCategoryDtos(subCategoryDtos, subCategories.Items, cancellationToken);
            return subCategoryDtos;
        }
    }
}