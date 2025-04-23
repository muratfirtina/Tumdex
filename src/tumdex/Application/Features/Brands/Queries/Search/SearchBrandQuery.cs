using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging; 

namespace Application.Features.Brands.Queries.Search;
public class SearchBrandQuery : IRequest<GetListResponse<BrandDto>>, ICachableRequest
{
    public string? SearchTerm { get; set; }
    // Arama genellikle ilk N sonucu döndürür, sayfalama isteğe bağlı eklenebilir.
    // public PageRequest PageRequest { get; set; } = new() { PageIndex = 0, PageSize = 10 };

    // ICachableRequest implementation
    // Arama terimine göre cache key. Sayfalama varsa eklenmeli.
    public string CacheKey => $"Brands-Search-{SearchTerm?.Trim().ToLower() ?? "all"}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Brands;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(15);
    
    public class SearchBrandQueryHandler : IRequestHandler<SearchBrandQuery, GetListResponse<BrandDto>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<SearchBrandQueryHandler> _logger;

        public SearchBrandQueryHandler(
            IBrandRepository brandRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<SearchBrandQueryHandler> logger)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<BrandDto>> Handle(SearchBrandQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Searching brands with term: '{SearchTerm}'", request.SearchTerm ?? "N/A");

            // Repository'den arama sonuçlarını al (IPaginate<Brand> döner)
            // SearchByNameAsync genellikle sayfalama parametreleri almalı.
            // Şimdilik varsayılan (ilk 10 gibi) döndürdüğünü varsayalım.
            IPaginate<Brand> brandsPaginated = await _brandRepository.SearchByNameAsync(request.SearchTerm); // Sayfalama eklenebilir

             if (brandsPaginated == null || brandsPaginated.Items == null) // Null kontrolü
             {
                _logger.LogWarning("SearchByNameAsync returned null or empty items for term: '{SearchTerm}'", request.SearchTerm ?? "N/A");
                return new GetListResponse<BrandDto> { Items = new List<BrandDto>() }; // Boş response
             }


            // IPaginate<Brand> -> GetListResponse<DTO> mapleme
            var response = _mapper.Map<GetListResponse<BrandDto>>(brandsPaginated);

            // Resim URL'lerini ayarla
            if (response.Items != null && response.Items.Any()) // Null kontrolü
            {
                foreach (var brandDto in response.Items)
                {
                     var brandEntity = brandsPaginated.Items.FirstOrDefault(b => b.Id == brandDto.Id);
                     if (brandEntity?.BrandImageFiles != null)
                     {
                         var brandImage = brandEntity.BrandImageFiles.FirstOrDefault();
                         if (brandImage != null)
                         {
                             brandDto.BrandImage = brandImage.ToDto(_storageService);
                         }
                     }
                }
            }

            _logger.LogInformation("Found {Count} brands matching term: '{SearchTerm}'", response.Count, request.SearchTerm ?? "N/A");
            return response;
        }
    }
}