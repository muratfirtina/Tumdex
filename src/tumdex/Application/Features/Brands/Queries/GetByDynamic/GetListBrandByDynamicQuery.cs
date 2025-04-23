using System.Text.Json;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Rules;
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
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace Application.Features.Brands.Queries.GetByDynamic;
public class GetListBrandByDynamicQuery : IRequest<GetListResponse<GetListBrandByDynamicQueryResponse>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }
    public string CacheKey => $"Brands-Dynamic-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}-{JsonSerializer.Serialize(DynamicQuery)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Brands;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetListByDynamicBrandQueryHandler : IRequestHandler<GetListBrandByDynamicQuery, GetListResponse<GetListBrandByDynamicQueryResponse>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly BrandBusinessRules _brandBusinessRules;
        private readonly ILogger<GetListByDynamicBrandQueryHandler> _logger;

        public GetListByDynamicBrandQueryHandler(
            IBrandRepository brandRepository,
            IMapper mapper,
            IStorageService storageService,
            BrandBusinessRules brandBusinessRules,
            ILogger<GetListByDynamicBrandQueryHandler> logger)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
            _brandBusinessRules = brandBusinessRules;
            _logger = logger;
        }

        public async Task<GetListResponse<GetListBrandByDynamicQueryResponse>> Handle(GetListBrandByDynamicQuery request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Executing GetListBrandByDynamicQuery. PageIndex: {PageIndex}, PageSize: {PageSize}, DynamicQuery: {DynamicQueryJson}",
                 request.PageRequest.PageIndex, request.PageRequest.PageSize, JsonSerializer.Serialize(request.DynamicQuery));


             GetListResponse<GetListBrandByDynamicQueryResponse> response;

             if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
             {
                 // Tüm sonuçları getir (sayfalama yok)
                 _logger.LogDebug("Fetching all brands matching dynamic query.");
                 var allBrands = await _brandRepository.GetAllByDynamicAsync(
                     request.DynamicQuery,
                     include: x => x.Include(x => x.BrandImageFiles) 
                                  .Include(x => x.Products),
                     cancellationToken: cancellationToken);

                  // List<Brand> -> List<DTO> mapleme
                 var brandDtos = _mapper.Map<List<GetListBrandByDynamicQueryResponse>>(allBrands);

                 // Resim URL'lerini ve ek bilgileri ayarla
                 foreach (var brandDto in brandDtos)
                 {
                     var brandEntity = allBrands.FirstOrDefault(b => b.Id == brandDto.Id);
                     if (brandEntity != null)
                     {
                         var brandImage = brandEntity.BrandImageFiles?.FirstOrDefault();
                         if (brandImage != null) brandDto.BrandImage = brandImage.ToDto(_storageService);
                         brandDto.ProductCount = brandEntity.Products?.Count ?? 0;
                     }
                 }

                 // GetListResponse oluştur
                 response = new GetListResponse<GetListBrandByDynamicQueryResponse>
                 {
                     Items = brandDtos,
                     Count = brandDtos.Count,
                     Index = -1, // Sayfalama yok
                     Size = -1,  // Sayfalama yok
                     Pages = brandDtos.Any() ? 1 : 0,
                     HasNext = false,
                     HasPrevious = false
                 };
                 _logger.LogInformation("Returned {Count} total brands matching dynamic query.", response.Count);
             }
             else
             {
                 // Sayfalama ile sonuçları getir
                 _logger.LogDebug("Fetching paginated brands matching dynamic query. Page: {PageIndex}, Size: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
                 IPaginate<Brand> brandsPaginated = await _brandRepository.GetListByDynamicAsync(
                     request.DynamicQuery,
                     include: x => x.Include(x => x.BrandImageFiles)
                                  .Include(x => x.Products),
                     index: request.PageRequest.PageIndex,
                     size: request.PageRequest.PageSize,
                     cancellationToken: cancellationToken);

                 // IPaginate<Brand> -> GetListResponse<DTO> mapleme
                 response = _mapper.Map<GetListResponse<GetListBrandByDynamicQueryResponse>>(brandsPaginated);

                 // Resim URL'lerini ve ek bilgileri ayarla
                 if (response.Items != null && response.Items.Any()) // Null kontrolü eklendi
                 {
                    foreach (var brandDto in response.Items)
                    {
                        // IPaginate.Items içinde orijinal entity'leri bul
                        var brandEntity = brandsPaginated.Items.FirstOrDefault(b => b.Id == brandDto.Id);
                        if (brandEntity != null)
                        {
                            var brandImage = brandEntity.BrandImageFiles?.FirstOrDefault();
                            if (brandImage != null) brandDto.BrandImage = brandImage.ToDto(_storageService);
                            brandDto.ProductCount = brandEntity.Products?.Count ?? 0;
                        }
                    }
                 }
                 _logger.LogInformation("Returned {Count} brands on page {PageIndex} matching dynamic query. Total items: {TotalCount}", response.Items?.Count ?? 0, response.Index, response.Count);
             }

             return response;
        }
    }
}