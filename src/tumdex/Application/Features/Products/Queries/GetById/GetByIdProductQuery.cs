using System.Security.Claims;
using Application.Abstraction.Services;
using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using AutoMapper.Internal;
using Core.Application.Pipelines.Caching;
using Core.CrossCuttingConcerns.Exceptions;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.GetById;

public class GetByIdProductQuery : IRequest<GetByIdProductResponse>, ICachableRequest
{
    public string Id { get; set; }

    // ICachableRequest implementation
    public string CacheKey => $"Product-{Id}"; // Standart ID bazlı key
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Ürün grubuna ait
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(60); // Tekil ürün 1 saat cache

    // --- Handler ---
    public class GetByIdProductQueryHandler : IRequestHandler<GetByIdProductQuery, GetByIdProductResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductLikeRepository _productLikeRepository; // Like count için
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetByIdProductQueryHandler> _logger; // Logger eklendi

        public GetByIdProductQueryHandler(
            IProductRepository productRepository,
            IProductLikeRepository productLikeRepository, // Eklendi
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetByIdProductQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _productLikeRepository = productLikeRepository; // Atandı
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger; // Atandı
        }

        public async Task<GetByIdProductResponse> Handle(GetByIdProductQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching product by ID: {ProductId}", request.Id);

            // Ana ürünü tüm detaylarıyla getir
            Product? product = await _productRepository.GetAsync(
                predicate: p => p.Id == request.Id,
                cancellationToken: cancellationToken,
                include: x => x.Include(x => x.Category) // CategoryName
                              .Include(x => x.Brand) // BrandName
                              .Include(x => x.ProductLikes) // LikeCount için (opsiyonel, ayrı query daha iyi olabilir)
                              .Include(x => x.ProductFeatureValues).ThenInclude(pfv => pfv.FeatureValue).ThenInclude(fv => fv.Feature) // Özellikler
                              .Include(x => x.ProductImageFiles)); // Tüm resimler

            if (product == null)
            {
                _logger.LogWarning("Product not found with ID: {ProductId}", request.Id);
                throw new BusinessException($"Product with ID '{request.Id}' not found.");
            }

            // İlişkili (varyant) ürünleri getir (ana ürün hariç)
             _logger.LogDebug("Fetching related products for VaryantGroupID: {VaryantGroupId}, excluding ProductId: {ProductId}", product.VaryantGroupID, product.Id);
            var relatedProductsPaginated = await _productRepository.GetListAsync(
                predicate: p => p.VaryantGroupID == product.VaryantGroupID && p.Id != product.Id,
                include: x => x.Include(rp => rp.Category)
                              .Include(rp => rp.Brand) // DTO'da yoksa gereksiz
                              .Include(rp => rp.ProductLikes) // DTO'da yoksa gereksiz
                              .Include(rp => rp.ProductFeatureValues).ThenInclude(pfv => pfv.FeatureValue).ThenInclude(fv => fv.Feature) // Özellikler önemli
                              .Include(rp => rp.ProductImageFiles.Where(pif => pif.Showcase == true)), // Sadece vitrin resmi
                 orderBy: q => q.OrderBy(rp => rp.Name), // Sıralama (opsiyonel)
                cancellationToken: cancellationToken);
             var relatedProducts = relatedProductsPaginated.Items.ToList(); // Sayfalamaya gerek yoksa liste al
             _logger.LogDebug("Found {Count} related products.", relatedProducts.Count);


            // Ana ürünü DTO'ya map'le
            GetByIdProductResponse response = _mapper.Map<GetByIdProductResponse>(product);

            // Ana ürün resimlerini DTO'ya ekle
            response.ProductImageFiles = product.ProductImageFiles?
                .Select(pif => pif.ToDto(_storageService))
                .ToList();
            response.LikeCount = product.ProductLikes?.Count ?? 0; // Like sayısını ata

            // İlişkili ürünleri DTO'ya map'le
            response.RelatedProducts = relatedProducts.Select(rp =>
            {
                var relatedDto = _mapper.Map<RelatedProductDto>(rp); // RelatedProductDto için mapping profili olmalı
                // İlişkili ürün vitrin resmini ayarla
                var showcaseImage = rp.ProductImageFiles?.FirstOrDefault(); // Showcase=true zaten filtrelendi
                if (showcaseImage != null)
                {
                    relatedDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
                return relatedDto;
            }).ToList();

            // Mevcut Özellikleri Hesapla (Varyant seçimi için)
            response.AvailableFeatures = CalculateAvailableFeatures(
                response.RelatedProducts, 
                response.ProductFeatureValues?.ToList() ?? new List<ProductFeatureValueDto>());

            _logger.LogInformation("Successfully fetched product details and related products for ID: {ProductId}", request.Id);
            return response;
        }

        // Yardımcı metod: İlişkili ürünlerden seçilebilir özellikleri çıkarır
        private Dictionary<string, List<string>> CalculateAvailableFeatures(
            List<RelatedProductDto> relatedProducts,
            List<ProductFeatureValueDto> currentProductFeatures)
        {
            var available = new Dictionary<string, List<string>>();
            var allProductsForFeatures = new List<RelatedProductDto>(relatedProducts);
            // Mevcut ürünü de ekleyerek tüm varyantları dahil et
            allProductsForFeatures.Add(new RelatedProductDto { ProductFeatureValues = currentProductFeatures });

            foreach (var relatedProduct in allProductsForFeatures)
            {
                if (relatedProduct.ProductFeatureValues == null) continue;

                foreach (var featureValue in relatedProduct.ProductFeatureValues)
                {
                    if (string.IsNullOrEmpty(featureValue.FeatureName) || string.IsNullOrEmpty(featureValue.FeatureValueName)) continue;

                    if (!available.ContainsKey(featureValue.FeatureName))
                    {
                        available[featureValue.FeatureName] = new List<string>();
                    }

                    if (!available[featureValue.FeatureName].Contains(featureValue.FeatureValueName))
                    {
                        available[featureValue.FeatureName].Add(featureValue.FeatureValueName);
                    }
                }
            }
            _logger.LogDebug("Calculated available features for variant selection.");
            return available;
        }
    }
}