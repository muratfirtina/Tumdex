using Application.Features.Products.Dtos;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Persistence.Repositories.Operation;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Application.Features.Products.Commands.Create;

public class CreateMultipleProductsCommand : IRequest<List<CreatedProductResponse>>, ICacheRemoverRequest // ITransactionalRequest eklenebilir
{
    public List<CreateMultipleProductDto> Products { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;
    
    public class CreateMultipleProductsCommandHandler
        : IRequestHandler<CreateMultipleProductsCommand, List<CreatedProductResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly ProductBusinessRules _productBusinessRules;
        private readonly IStorageService _storageService;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly IBrandRepository _brandRepository;
        private readonly ILogger<CreateMultipleProductsCommandHandler> _logger;

        public CreateMultipleProductsCommandHandler(
            IProductRepository productRepository,
            IMapper mapper,
            ProductBusinessRules productBusinessRules,
            IStorageService storageService,
            ICategoryRepository categoryRepository,
            IFeatureValueRepository featureValueRepository,
            IBrandRepository brandRepository,
            ILogger<CreateMultipleProductsCommandHandler> logger)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _productBusinessRules = productBusinessRules;
            _storageService = storageService;
            _categoryRepository = categoryRepository;
            _featureValueRepository = featureValueRepository;
            _brandRepository = brandRepository;
            _logger = logger;
        }

        public async Task<List<CreatedProductResponse>> Handle(
            CreateMultipleProductsCommand request,
            CancellationToken cancellationToken)
        {
            if (request.Products == null || !request.Products.Any())
            {
                _logger.LogWarning("CreateMultipleProductsCommand called with no products.");
                return new List<CreatedProductResponse>();
            }

            _logger.LogInformation("Starting bulk product creation for {Count} products.", request.Products.Count);
            var responses = new List<CreatedProductResponse>();
            var varyantGroupCache = new Dictionary<string, string>(); // Varyant grupları için cache

            // --- Ön Kontroller (Transaction Dışı) ---
            var categoryIds = request.Products.Select(p => p.CategoryId).Distinct().ToList();
            var brandIds = request.Products.Select(p => p.BrandId).Distinct().ToList();
            var featureValueIds = request.Products.Where(p => p.FeatureValueIds != null).SelectMany(p => p.FeatureValueIds).Distinct().ToList();

            await _productBusinessRules.CategoriesShouldExistWhenSelected(categoryIds, cancellationToken);
            await _productBusinessRules.BrandsShouldExistWhenSelected(brandIds, cancellationToken);
            if (featureValueIds.Any())
                await _productBusinessRules.FeatureValuesShouldExistWhenSelected(featureValueIds, cancellationToken);
            _logger.LogInformation("Pre-checks passed for categories, brands, and feature values.");
            // --- Ön Kontroller Bitti ---


            foreach (var productDto in request.Products)
            {
                try
                {
                    // Business Rules (Her ürün için) - Örneğin SKU benzersizliği kontrol edilebilir.
                    //await _productBusinessRules.ProductSkuShouldBeUnique(productDto.Sku, null, cancellationToken); // null ID -> create kontrolü

                    var product = _mapper.Map<Product>(productDto);

                    // Varyant Grup ID
                    var normalizeName = NameOperation.CharacterRegulatory(productDto.Name);
                    if (!string.IsNullOrEmpty(productDto.VaryantGroupID))
                    {
                        product.VaryantGroupID = productDto.VaryantGroupID;
                    }
                    else
                    {
                        if (varyantGroupCache.TryGetValue(normalizeName, out var cachedGroupId))
                        {
                            product.VaryantGroupID = cachedGroupId;
                        }
                        else
                        {
                            var randomCode = GenerateRandomCode(8);
                            product.VaryantGroupID = $"{normalizeName}-{randomCode}";
                            varyantGroupCache[normalizeName] = product.VaryantGroupID;
                        }
                    }
                     _logger.LogDebug("Assigned VaryantGroupID: {VaryantGroupId} for product SKU: {Sku}", product.VaryantGroupID, product.Sku);

                    // Feature Values İlişkisi
                    if (productDto.FeatureValueIds != null && productDto.FeatureValueIds.Any())
                    {
                        product.ProductFeatureValues = productDto.FeatureValueIds
                            .Select(fvId => new ProductFeatureValue { ProductId = product.Id, FeatureValueId = fvId })
                            .ToList();
                         _logger.LogDebug("Assigned {Count} feature values for product SKU: {Sku}", product.ProductFeatureValues.Count, product.Sku);
                    }

                    // Ürün Ekleme (Context'e)
                    await _productRepository.AddAsync(product); // SaveChanges transaction sonunda olacak
                    _logger.LogInformation("Product entity added to context. SKU: {Sku}, Temp ID: {TempId}", product.Sku, product.Id);


                    // Resim Yükleme ve İlişkilendirme
                    if (productDto.ProductImages != null && productDto.ProductImages.Any())
                    {
                        _logger.LogInformation("Uploading {Count} images for product SKU: {Sku}", productDto.ProductImages.Count, product.Sku);
                        var uploadedFiles = await _storageService.UploadAsync("products", product.Id, productDto.ProductImages);

                        // Eğer ShowcaseImageIndex null ise veya geçersiz bir değerse 0 (ilk resim) olarak ayarla
                        int showcaseIndex = productDto.ShowcaseImageIndex ?? 0;
                        if (showcaseIndex < 0 || showcaseIndex >= uploadedFiles.Count)
                            showcaseIndex = 0;

                        product.ProductImageFiles = uploadedFiles.Select((file, index) =>
                            new ProductImageFile(file.fileName, file.entityType, file.path, file.storageType)
                            {
                                Showcase = index == showcaseIndex,
                                Format = file.format
                            }).ToList();

                        // Vitrin resmi kontrolü
                        await _productBusinessRules.EnsureOnlyOneShowcaseImage(product.ProductImageFiles);
                    }

                    // Response için map'leme (henüz ID final değil, ama DTO bunu handle etmeli)
                    var responseDto = _mapper.Map<CreatedProductResponse>(product);
                    responses.Add(responseDto);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating product with SKU: {Sku}, Name: {ProductName}", productDto.Sku, productDto.Name);
                    // Hata durumunda ne yapılmalı? İşlemi durdurup rollback mi, yoksa devam edip hatalıları loglamak mı?
                    // TransactionalRequest varsa, tümü rollback olur. Yoksa, eklenenler kalır.
                    // Bu durumda hata fırlatmak transaction'ı rollback ettirecektir.
                    throw; // Hata durumunda işlemi durdur ve dışarıya bildir.
                }
            }

            // SaveChanges burada transaction ile otomatik çağrılır (eğer TransactionalBehaviour kullanılıyorsa).
            _logger.LogInformation("Bulk product creation process completed. Successfully prepared {Count} products.", responses.Count);
            return responses;
        }

        private string GenerateRandomCode(int length)
        {
            const string chars = "0123456789"; // Sadece rakam
            var randomBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[randomBytes[i] % chars.Length];
            }
            return new string(result);
        }
    }
}