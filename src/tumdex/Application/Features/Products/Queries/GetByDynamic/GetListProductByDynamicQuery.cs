using System.Text.Json;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetByDynamic;

public class GetListProductByDynamicQuery : IRequest<GetListResponse<GetListProductByDynamicDto>>, ICachableRequest
{
    public PageRequest? PageRequest { get; set; }
    public DynamicQuery? DynamicQuery { get; set; }

    // ICachableRequest implementation
    // Dinamik sorguyu serialize et
    public string CacheKey =>
        $"Products-Dynamic-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}-{JsonSerializer.Serialize(DynamicQuery)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün grubuna ait.
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(15); // Dinamik sorgu 15 dk cache.

    // --- Handler ---
    public class GetListByDynamicProductQueryHandler : IRequestHandler<GetListProductByDynamicQuery, GetListResponse<GetListProductByDynamicDto>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ProductBusinessRules _productBusinessRules; // Kullanılmıyor gibi
        private readonly ILogger<GetListByDynamicProductQueryHandler> _logger; // Logger eklendi

        public GetListByDynamicProductQueryHandler(
            IProductRepository productRepository,
            IMapper mapper,
            IStorageService storageService,
            ProductBusinessRules productBusinessRules,
            ILogger<GetListByDynamicProductQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _storageService = storageService;
            _productBusinessRules = productBusinessRules;
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetListProductByDynamicDto>> Handle(GetListProductByDynamicQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing GetListProductByDynamicQuery. PageIndex: {PageIndex}, PageSize: {PageSize}, DynamicQuery: {DynamicQueryJson}",
                 request.PageRequest.PageIndex, request.PageRequest.PageSize, JsonSerializer.Serialize(request.DynamicQuery));

            // Include DTO'ya göre ayarlanmalı
            Func<IQueryable<Product>, IIncludableQueryable<Product, object>> includeFunc = p => p
                .Include(e => e.Category) // CategoryName için
                .Include(e => e.Brand) // BrandName için
                .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature) // DTO'da yoksa gereksiz
                .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true)); // Resim için

             GetListResponse<GetListProductByDynamicDto> response;

            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                 // Tümünü getir
                 _logger.LogDebug("Fetching all products matching dynamic query.");
                 var allProducts = await _productRepository.GetAllByDynamicAsync(
                     request.DynamicQuery,
                     include: includeFunc,
                     cancellationToken: cancellationToken);

                 // List<Product> -> List<DTO>
                 var productDtos = _mapper.Map<List<GetListProductByDynamicDto>>(allProducts);

                 // Resim ayarla
                 foreach (var productDto in productDtos)
                 {
                     var productEntity = allProducts.FirstOrDefault(p => p.Id == productDto.Id);
                     if (productEntity?.ProductImageFiles != null)
                     {
                         var showcaseImage = productEntity.ProductImageFiles.FirstOrDefault(); // Showcase true olan zaten filtrelendi
                         if (showcaseImage != null)
                         {
                             productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                         }
                     }
                     // CategoryName ve BrandName AutoMapper profilinde maplenmeli.
                 }

                 // GetListResponse oluştur
                 response = new GetListResponse<GetListProductByDynamicDto>
                 {
                     Items = productDtos,
                     Count = productDtos.Count,
                     Index = -1, Size = -1, Pages = productDtos.Any() ? 1 : 0, HasNext = false, HasPrevious = false
                 };
                 _logger.LogInformation("Returned {Count} total products matching dynamic query.", response.Count);
            }
            else
            {
                 // Sayfalı getir
                  _logger.LogDebug("Fetching paginated products matching dynamic query. Page: {PageIndex}, Size: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
                 IPaginate<Product> productsPaginated = await _productRepository.GetListByDynamicAsync(
                     request.DynamicQuery,
                     include: includeFunc,
                     index: request.PageRequest.PageIndex,
                     size: request.PageRequest.PageSize,
                     cancellationToken: cancellationToken);

                 // IPaginate -> GetListResponse
                 response = _mapper.Map<GetListResponse<GetListProductByDynamicDto>>(productsPaginated);

                  // Resim ayarla
                 if (response.Items != null && response.Items.Any()) // Null kontrolü
                 {
                     foreach (var productDto in response.Items)
                     {
                         var productEntity = productsPaginated.Items.FirstOrDefault(p => p.Id == productDto.Id);
                         if (productEntity?.ProductImageFiles != null)
                         {
                             var showcaseImage = productEntity.ProductImageFiles.FirstOrDefault();
                             if (showcaseImage != null)
                             {
                                 productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                             }
                         }
                         // CategoryName ve BrandName AutoMapper profilinde maplenmeli.
                     }
                 }
                  _logger.LogInformation("Returned {Count} products on page {PageIndex} matching dynamic query. Total items: {TotalCount}", response.Items?.Count ?? 0, response.Index, response.Count);
            }

            return response;
        }
    }
}