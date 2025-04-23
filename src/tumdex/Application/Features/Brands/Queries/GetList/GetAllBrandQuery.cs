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

namespace Application.Features.Brands.Queries.GetList;
public class GetAllBrandQuery : IRequest<GetListResponse<GetAllBrandQueryResponse>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public string CacheKey => $"Brands-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Brands;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    public class GetAllBrandQueryHandler : IRequestHandler<GetAllBrandQuery, GetListResponse<GetAllBrandQueryResponse>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetAllBrandQueryHandler> _logger;

        public GetAllBrandQueryHandler(
            IBrandRepository brandRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetAllBrandQueryHandler> logger)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetListResponse<GetAllBrandQueryResponse>> Handle(GetAllBrandQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing GetAllBrandQuery. PageIndex: {PageIndex}, PageSize: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);

             GetListResponse<GetAllBrandQueryResponse> response;

             if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
             {
                 // Tüm markaları getir (sayfalama yok)
                  _logger.LogDebug("Fetching all brands.");
                 List<Brand> brands = await _brandRepository.GetAllAsync(
                     include: q => q.Include(b => b.BrandImageFiles),
                     orderBy: q => q.OrderBy(b => b.Name),
                     cancellationToken: cancellationToken);
                 
                 var brandDtos = _mapper.Map<List<GetAllBrandQueryResponse>>(brands);

                  // Resim URL'lerini ve ek bilgileri ayarla
                  foreach (var brandDto in brandDtos)
                  {
                      var brandEntity = brands.FirstOrDefault(b => b.Id == brandDto.Id);
                      if (brandEntity != null)
                      {
                          var brandImage = brandEntity.BrandImageFiles?.FirstOrDefault();
                          if (brandImage != null) brandDto.BrandImage = brandImage.ToDto(_storageService);
                          brandDto.ProductCount = brandEntity.Products?.Count ?? 0;
                      }
                  }

                  // GetListResponse oluştur
                 response = new GetListResponse<GetAllBrandQueryResponse>
                 {
                     Items = brandDtos,
                     Count = brandDtos.Count,
                     Index = -1, Size = -1, Pages = brandDtos.Any() ? 1 : 0, HasNext = false, HasPrevious = false
                 };
                 _logger.LogInformation("Returned {Count} total brands.", response.Count);
             }
             else
             {
                  // Sayfalı olarak getir
                  _logger.LogDebug("Fetching paginated brands. Page: {PageIndex}, Size: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
                 IPaginate<Brand> brandsPaginated = await _brandRepository.GetListAsync(
                     index: request.PageRequest.PageIndex,
                     size: request.PageRequest.PageSize,
                     include: q => q.Include(b => b.BrandImageFiles)
                         .Include(b => b.Products),
                     orderBy: q => q.OrderBy(b => b.Name),
                     cancellationToken: cancellationToken
                 );

                 // IPaginate<Brand> -> GetListResponse<DTO> mapleme
                 response = _mapper.Map<GetListResponse<GetAllBrandQueryResponse>>(brandsPaginated);

                 // Resim URL'lerini ve ek bilgileri ayarla
                 if (response.Items != null && response.Items.Any()) // Null kontrolü eklendi
                 {
                    foreach (var brandDto in response.Items)
                    {
                         var brandEntity = brandsPaginated.Items.FirstOrDefault(b => b.Id == brandDto.Id);
                         if (brandEntity != null)
                         {
                             var brandImage = brandEntity.BrandImageFiles?.FirstOrDefault();
                             if (brandImage != null) brandDto.BrandImage = brandImage.ToDto(_storageService);
                             brandDto.ProductCount = brandEntity.Products?.Count ?? 0;
                         }
                    }
                 }
                  _logger.LogInformation("Returned {Count} brands on page {PageIndex}. Total items: {TotalCount}", response.Items?.Count ?? 0, response.Index, response.Count);
             }

             return response;
        }
    }
}