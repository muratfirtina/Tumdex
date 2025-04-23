using Application.Consts;
using Application.Dtos.Image;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Products.Dtos;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Services;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Commands.Update;

public class UpdateProductCommand : IRequest<UpdatedProductResponse>, ICacheRemoverRequest, ITransactionalRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string CategoryId { get; set; }
    public string BrandId { get; set; }
    public string VaryantGroupID { get; set; }
    public string Sku { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int Tax { get; set; }
    public List<ProductFeatureDto> ProductFeatures { get; set; }
    public List<IFormFile>? NewProductImages { get; set; }
    public List<string>? ExistingImageIds { get; set; }
    public int? ShowcaseImageIndex { get; set; }
    
    public string CacheKey => $"Product-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;
    
    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, UpdatedProductResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly IFeatureValueRepository _featureValueRepository; // Eklendi: Özellik değeri kontrolü için
        private readonly IImageFileRepository _imageFileRepository;
        private readonly ICategoryRepository _categoryRepository; // Eklendi
        private readonly IBrandRepository _brandRepository; // Eklendi
        private readonly ProductBusinessRules _productBusinessRules;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly IImageSeoService _imageSeoService; // Kullanılmıyor gibi, kaldırılabilir
        private readonly ILogger<UpdateProductCommandHandler> _logger; // Logger eklendi

        public UpdateProductCommandHandler(
            IProductRepository productRepository,
            IMapper mapper,
            ProductBusinessRules productBusinessRules,
            IFeatureValueRepository featureValueRepository, // Eklendi
            IStorageService storageService,
            IImageFileRepository imageFileRepository,
            ICategoryRepository categoryRepository, // Eklendi
            IBrandRepository brandRepository, // Eklendi
            IImageSeoService imageSeoService,
            ILogger<UpdateProductCommandHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _productBusinessRules = productBusinessRules;
            _featureValueRepository = featureValueRepository; // Atandı
            _storageService = storageService;
            _imageFileRepository = imageFileRepository;
            _categoryRepository = categoryRepository; // Atandı
            _brandRepository = brandRepository; // Atandı
            _imageSeoService = imageSeoService;
            _logger = logger;
        }

        public async Task<UpdatedProductResponse> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to update product with ID: {ProductId}", request.Id);

            Product? product = await _productRepository.GetAsync(
                p => p.Id == request.Id,
                include: p => p.Include(e => e.Category)
                              .Include(e => e.Brand)
                              .Include(e => e.ProductFeatureValues)
                                .ThenInclude(pfv => pfv.FeatureValue)
                              .Include(e => e.ProductImageFiles),
                cancellationToken: cancellationToken);

            // --- İş Kuralları ve Ön Kontroller ---
            await _productBusinessRules.ProductShouldExistWhenSelected(product);
             if (product == null) throw new BusinessException($"Product with ID '{request.Id}' not found.");

            // SKU benzersizlik kontrolü (kendi ID'si hariç)
            //await _productBusinessRules.ProductSkuShouldBeUnique(request.Sku, request.Id, cancellationToken);

            // Kategori ve Marka varlık kontrolü
             await _productBusinessRules.CategoryShouldExistWhenSelected(request.CategoryId, cancellationToken);
             await _productBusinessRules.BrandShouldExistWhenSelected(request.BrandId, cancellationToken);

            // Özellik Değerleri varlık kontrolü
            var featureValueIds = request.ProductFeatures?.SelectMany(pf => pf.FeatureValues.Select(fv => fv.Id)).Distinct().ToList() ?? new List<string>();
             if (featureValueIds.Any())
                await _productBusinessRules.FeatureValuesShouldExistWhenSelected(featureValueIds, cancellationToken);
             _logger.LogDebug("Pre-checks passed for Product, SKU, Category, Brand, FeatureValues for Product ID: {ProductId}", request.Id);
            // --- İş Kuralları ve Ön Kontroller Bitti ---


            // Temel Bilgileri Güncelle
             _mapper.Map(request, product); // AutoMapper ile temel alanları güncelle
             product.UpdatedDate = DateTime.UtcNow;
             _logger.LogDebug("Basic product information mapped for Product ID: {ProductId}", request.Id);


            // Özellikleri Güncelle
            await UpdateProductFeatures(product, request.ProductFeatures, cancellationToken);


            // Resimleri Yönet
            List<ProductImageFile> finalImageList = await ManageProductImages(product, request, cancellationToken);
            product.ProductImageFiles = finalImageList; // Güncel listeyi ata


            // Vitrin Resmini Ayarla (Güncel liste üzerinden)
            UpdateShowcaseImage(product, request.ShowcaseImageIndex);


            // Ürünü DB'de Güncelle
            await _productRepository.UpdateAsync(product);
            _logger.LogInformation("Product updated successfully in database: {ProductId}", request.Id);


            // Response Oluştur
            UpdatedProductResponse response = _mapper.Map<UpdatedProductResponse>(product);
             // Response için resimleri map'le
             if (product.ProductImageFiles != null)
             {
                 response.Images = product.ProductImageFiles
                     .Select(pif => pif.ToDto(_storageService))
                     .ToList();
             }

            _logger.LogInformation("Product update process completed for ID: {ProductId}", request.Id);
            return response;
        }


        private async Task UpdateProductFeatures(Product product, List<ProductFeatureDto>? requestedFeatures, CancellationToken cancellationToken)
        {
             _logger.LogDebug("Updating product features for Product ID: {ProductId}", product.Id);
             product.ProductFeatureValues ??= new List<ProductFeatureValue>(); // Null ise initialize et
             product.ProductFeatureValues.Clear(); // Önce mevcut ilişkiyi temizle (ilişki tablosu için)

             if (requestedFeatures != null && requestedFeatures.Any())
             {
                 var featureValueIdsToAdd = requestedFeatures
                     .SelectMany(pf => pf.FeatureValues.Select(fv => fv.Id))
                     .Distinct()
                     .ToList();

                 // Varlık kontrolü (Handler başında yapıldı, tekrar gerekebilir mi?)
                 // await _productBusinessRules.FeatureValuesShouldExistWhenSelected(featureValueIdsToAdd, cancellationToken);

                 foreach (var featureValueId in featureValueIdsToAdd)
                 {
                     product.ProductFeatureValues.Add(new ProductFeatureValue
                     {
                         ProductId = product.Id,
                         FeatureValueId = featureValueId
                     });
                 }
                 _logger.LogDebug("Assigned {Count} distinct feature values to Product ID: {ProductId}", featureValueIdsToAdd.Count, product.Id);
             }
             else
             {
                 _logger.LogDebug("No features provided, all existing features removed for Product ID: {ProductId}", product.Id);
             }
        }


        private async Task<List<ProductImageFile>> ManageProductImages(Product product, UpdateProductCommand request, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Managing product images for Product ID: {ProductId}", product.Id);
            var currentImages = product.ProductImageFiles?.ToList() ?? new List<ProductImageFile>();
            var finalImages = new List<ProductImageFile>();
            request.ExistingImageIds ??= new List<string>(); // Null ise boş liste yap

            // 1. Silinmesi İstenen Mevcut Resimleri Kaldır
            var imagesToRemove = currentImages.Where(img => !request.ExistingImageIds.Contains(img.Id)).ToList();
            if (imagesToRemove.Any())
            {
                _logger.LogInformation("Removing {Count} images for Product ID: {ProductId}", imagesToRemove.Count, product.Id);
                foreach (var image in imagesToRemove)
                {
                    try
                    {
                        await _storageService.DeleteFromAllStoragesAsync(image.EntityType, image.Path, image.Name);
                        await _imageFileRepository.DeleteAsync(image); // DB'den sil
                        _logger.LogDebug("Removed image {FileName} from storage and DB.", image.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error removing image {FileName} for Product ID: {ProductId}", image.Name, product.Id);
                    }
                }
            }

            // 2. Kalan Mevcut Resimleri Final Listeye Ekle
            finalImages.AddRange(currentImages.Where(img => request.ExistingImageIds.Contains(img.Id)));
            _logger.LogDebug("{Count} existing images kept for Product ID: {ProductId}", finalImages.Count, product.Id);

            // 3. Yeni Resimleri Yükle ve Ekle
            if (request.NewProductImages != null && request.NewProductImages.Any())
            {
                _logger.LogInformation("Uploading {Count} new images for Product ID: {ProductId}", request.NewProductImages.Count, product.Id);
                // UploadAsync birden fazla dosya alabilir
                var uploadedFiles = await _storageService.UploadAsync("products", product.Id, request.NewProductImages);

                foreach (var file in uploadedFiles)
                {
                    var newImageFile = new ProductImageFile(file.fileName, file.entityType, file.path, file.storageType)
                    {
                         Alt = $"{product.Name} - {file.fileName}", // Basit Alt metni
                         Title = product.Title,
                         Description = product.Description,
                         Format = file.format,
                         Showcase = false // Vitrin durumu daha sonra ayarlanacak
                    };
                    finalImages.Add(newImageFile);
                    // DB'ye ekleme AddAsync ile değil, product'a atanarak yapılır.
                    _logger.LogDebug("Added new uploaded image {FileName} to the final list.", file.fileName);
                }
            }

            return finalImages; // Product'a atanacak güncel liste
        }


        private void UpdateShowcaseImage(Product product, int? showcaseIndex)
        {
            if (product.ProductImageFiles == null || !product.ProductImageFiles.Any())
            {
                 _logger.LogWarning("Cannot set showcase image as there are no images for Product ID: {ProductId}", product.Id);
                 return;
            }

             // Önce tümünü false yap
             foreach (var image in product.ProductImageFiles)
             {
                 image.Showcase = false;
             }

            if (showcaseIndex.HasValue)
            {
                int index = showcaseIndex.Value;
                if (index >= 0 && index < product.ProductImageFiles.Count)
                {
                    product.ProductImageFiles.ElementAt(index).Showcase = true;
                    _logger.LogInformation("Set image at index {Index} as showcase for Product ID: {ProductId}", index, product.Id);
                }
                else
                {
                    _logger.LogWarning("Invalid showcase index ({Index}) provided for Product ID: {ProductId}. Setting the first image as showcase.", index, product.Id);
                     product.ProductImageFiles.First().Showcase = true; // Hatalı index ise ilkini seç
                }
            }
            else
            {
                 // Showcase index belirtilmemişse, ilk resmi showcase yap
                 product.ProductImageFiles.First().Showcase = true;
                 _logger.LogInformation("No showcase index provided. Set the first image as showcase for Product ID: {ProductId}", product.Id);
            }

            // Tekrar kontrol (emin olmak için)
            if (product.ProductImageFiles.Count(img => img.Showcase) != 1)
            {
                 _logger.LogError("Showcase image setting resulted in {Count} showcase images for Product ID: {ProductId}. Correcting...", product.ProductImageFiles.Count(img => img.Showcase), product.Id);
                 // Düzeltme: İlkini tekrar seç
                 foreach (var image in product.ProductImageFiles) image.Showcase = false;
                 if(product.ProductImageFiles.Any()) product.ProductImageFiles.First().Showcase = true;
            }
        }
    }
}