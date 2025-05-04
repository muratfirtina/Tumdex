using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetList;

public class GetAllProductQuery : IRequest<GetListResponse<GetAllProductQueryResponse>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public string CacheKey => $"Products-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}"; // Standart sayfalama key
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün grubuna ait
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(20); // Genel liste 20 dk cache
    
    public class GetAllProductQueryHandler : IRequestHandler<GetAllProductQuery, GetListResponse<GetAllProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductLikeRepository _productLikeRepository; // Kullanılmıyor gibi, LikeCount için gerekli
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<GetAllProductQueryHandler> _logger; // Logger eklendi

        public GetAllProductQueryHandler(
            IProductRepository productRepository,
            IMapper mapper,
            IStorageService storageService,
            IProductLikeRepository productLikeRepository, // Eklendi
            ILogger<GetAllProductQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _storageService = storageService;
            _productLikeRepository = productLikeRepository; // Atandı
            _logger = logger; // Atandı
        }

        public async Task<GetListResponse<GetAllProductQueryResponse>> Handle(GetAllProductQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing GetAllProductQuery. PageIndex: {PageIndex}, PageSize: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);

            // Include DTO'ya göre ayarlanmalı
            Func<IQueryable<Product>, IIncludableQueryable<Product, object>> includeFunc = x => x
                .Include(p => p.Category) // CategoryName
                .Include(p => p.Brand) // BrandName
                .Include(p => p.ProductLikes) // LikeCount
                .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature) // Uncomment this
                .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true));

            GetListResponse<GetAllProductQueryResponse> response;

            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                // Tümünü getir
                _logger.LogDebug("Fetching all products.");
                List<Product> products = await _productRepository.GetAllAsync(
                    include: includeFunc,
                    orderBy: q => q.OrderBy(p => p.Name), // İsme göre sıralı
                    cancellationToken: cancellationToken);

                // List<Product> -> List<DTO>
                var productDtos = _mapper.Map<List<GetAllProductQueryResponse>>(products);

                // Resim ve ek bilgi (LikeCount)
                foreach (var productDto in productDtos)
                {
                    var productEntity = products.FirstOrDefault(p => p.Id == productDto.Id);
                    if (productEntity != null)
                    {
                        var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault(); // Showcase true filtrelendi
                        if (showcaseImage != null) productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                        productDto.LikeCount = productEntity.ProductLikes?.Count ?? 0;
                        // CategoryName, BrandName AutoMapper profilinden gelmeli.
                    }
                }

                // GetListResponse oluştur
                response = new GetListResponse<GetAllProductQueryResponse>
                {
                    Items = productDtos,
                    Count = productDtos.Count,
                    Index = -1, Size = -1, Pages = productDtos.Any() ? 1 : 0, HasNext = false, HasPrevious = false
                };
                 _logger.LogInformation("Returned {Count} total products.", response.Count);
            }
            else
            {
                 // Sayfalı getir
                 _logger.LogDebug("Fetching paginated products. Page: {PageIndex}, Size: {PageSize}", request.PageRequest.PageIndex, request.PageRequest.PageSize);
                IPaginate<Product> productsPaginated = await _productRepository.GetListAsync(
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    include: includeFunc,
                     orderBy: q => q.OrderBy(p => p.Name), // İsme göre sıralı
                    cancellationToken: cancellationToken
                );

                // IPaginate -> GetListResponse
                response = _mapper.Map<GetListResponse<GetAllProductQueryResponse>>(productsPaginated);

                // Resim ve ek bilgi (LikeCount)
                 if (response.Items != null && response.Items.Any()) // Null kontrolü
                 {
                    foreach (var productDto in response.Items)
                    {
                        var productEntity = productsPaginated.Items.FirstOrDefault(p => p.Id == productDto.Id);
                        if (productEntity != null)
                        {
                            var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault();
                            if (showcaseImage != null) productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                            productDto.LikeCount = productEntity.ProductLikes?.Count ?? 0;
                             // CategoryName, BrandName AutoMapper profilinden gelmeli.
                        }
                    }
                 }
                  _logger.LogInformation("Returned {Count} products on page {PageIndex}. Total items: {TotalCount}", response.Items?.Count ?? 0, response.Index, response.Count);
            }

            return response;
        }
    }
}