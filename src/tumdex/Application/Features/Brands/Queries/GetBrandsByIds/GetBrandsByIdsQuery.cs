// Application/Features/Brands/Queries/GetBrandsByIds/GetBrandsByIdsQuery.cs
using Application.Consts;
using Application.Dtos.Image; // ImageDto için
using Application.Extensions.ImageFileExtensions; // ToDto için
using Application.Features.Brands.Dtos; // GetBrandsByIdsQueryResponse için (varsa)
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests; // PageRequest (kullanılmıyor ama GetListResponse için referans)
using Core.Application.Responses;
using Domain; // GetBrandsByIdsQueryResponse için (varsa)
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Logger için
using System.Collections.Generic; // List için
using System.Linq; // OrderBy için

namespace Application.Features.Brands.Queries.GetBrandsByIds;
public class GetBrandsByIdsQuery : IRequest<GetListResponse<GetBrandsByIdsQueryResponse>>, ICachableRequest
{
    public List<string> Ids { get; set; }

    // ICachableRequest implementation
    // CacheKey: ID'leri sıralayarak deterministik hale getiriyoruz.
    public string CacheKey => $"Brands-ByIds-{string.Join("_", Ids?.OrderBy(id => id) ?? Enumerable.Empty<string>())}";
    public bool BypassCache => false; // Cache'i atlama.
    public string? CacheGroupKey => CacheGroups.Brands; // Bu sorgu Markalar grubuna aittir.
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(60); // Marka bilgisi genellikle statiktir, 1 saat cache.

    // --- Handler ---
    public class GetBrandsByIdsQueryHandler : IRequestHandler<GetBrandsByIdsQuery, GetListResponse<GetBrandsByIdsQueryResponse>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetBrandsByIdsQueryHandler> _logger;

        public GetBrandsByIdsQueryHandler(
            IBrandRepository brandRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetBrandsByIdsQueryHandler> logger)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<GetBrandsByIdsQueryResponse>> Handle(GetBrandsByIdsQuery request, CancellationToken cancellationToken)
        {
             if (request.Ids == null || !request.Ids.Any())
             {
                 _logger.LogWarning("GetBrandsByIdsQuery called with no IDs provided.");
                 // Boş ID listesi için boş response döndür
                 return new GetListResponse<GetBrandsByIdsQueryResponse> { Items = new List<GetBrandsByIdsQueryResponse>() };
             }

             _logger.LogInformation("Fetching brands for IDs: {BrandIds}", string.Join(", ", request.Ids));
             
             var brands = await _brandRepository.GetAllAsync(
                 predicate: x => request.Ids.Contains(x.Id),
                 include: query => query
                     .Include(b => b.BrandImageFiles)
                     .Include(b => b.Products),
                index: -1,
                size: -1,
                 cancellationToken: cancellationToken
             );

             if (brands == null || !brands.Any())
             {
                 _logger.LogWarning("No brands found for the provided IDs: {BrandIds}", string.Join(", ", request.Ids));
             }

             // AutoMapper ile DTO listesine dönüştür
             // DİKKAT: `_mapper.Map` doğrudan List<Brand> alıp GetListResponse<T> döndürmez.
             // Önce List<TDestination> yapıp sonra GetListResponse içine koymak gerekir.
             var brandDtos = _mapper.Map<List<GetBrandsByIdsQueryResponse>>(brands); // varsayılan List<Brand> -> List<DTO> mapping

             // Resim URL'lerini ve ek bilgileri (örn. ProductCount) ayarla
             foreach (var brandDto in brandDtos)
             {
                 var brandEntity = brands.FirstOrDefault(b => b.Id == brandDto.Id); // Orijinal entity'yi bul
                 if (brandEntity != null)
                 {
                     // Resmi ayarla
                     var brandImage = brandEntity.BrandImageFiles?.FirstOrDefault(); // İlk resmi al (veya showcase olanı)
                     if (brandImage != null)
                     {
                         brandDto.BrandImage = brandImage.ToDto(_storageService);
                     }
                     // Ürün sayısını ayarla (opsiyonel)
                     brandDto.ProductCount = brandEntity.Products?.Count ?? 0;
                 }
             }

             // GetListResponse oluştur
             var response = new GetListResponse<GetBrandsByIdsQueryResponse>
             {
                 Items = brandDtos,
                 // Index, Size, Count gibi bilgiler bu sorgu tipi için anlamsız olabilir.
                 // İstenirse ayarlanabilir.
                 Count = brandDtos.Count
             };

             _logger.LogInformation("Returning {Count} brands for IDs: {BrandIds}", response.Count, string.Join(", ", request.Ids));
             return response;
        }
    }
}