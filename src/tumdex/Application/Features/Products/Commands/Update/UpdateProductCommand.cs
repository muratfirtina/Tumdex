using Application.Consts;
using Application.Dtos.Image;
using Application.Features.Products.Dtos;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Services;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.Update;

public class UpdateProductCommand : IRequest<UpdatedProductResponse>, ICacheRemoverRequest
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
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    

    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, UpdatedProductResponse>
{
    private readonly IProductRepository _productRepository;
    private readonly IFeatureRepository _featureRepository;
    private readonly IImageFileRepository _imageFileRepository;
    private readonly ProductBusinessRules _productBusinessRules;
    private readonly IMapper _mapper;
    private readonly IStorageService _storageService;
    private readonly IImageSeoService _imageSeoService; // Yeni servis eklendi

    public UpdateProductCommandHandler(
        IProductRepository productRepository, 
        IMapper mapper, 
        ProductBusinessRules productBusinessRules, 
        IFeatureRepository featureRepository,
        IStorageService storageService, 
        IImageFileRepository imageFileRepository,
        IImageSeoService imageSeoService)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _productBusinessRules = productBusinessRules;
        _featureRepository = featureRepository;
        _storageService = storageService;
        _imageFileRepository = imageFileRepository;
        _imageSeoService = imageSeoService;
    }

    public async Task<UpdatedProductResponse> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        Product? product = await _productRepository.GetAsync(
            p => p.Id == request.Id,
            include: p => p.Include(e => e.Category)
                .Include(e => e.Brand)
                .Include(e => e.ProductFeatureValues)
                .Include(e => e.ProductImageFiles),
            cancellationToken: cancellationToken);

        await _productBusinessRules.ProductShouldExistWhenSelected(product);

        if (product != null)
        {
            // Temel bilgileri güncelle
            UpdateBasicInformation(product, request);

            // Özellikleri güncelle
            await UpdateProductFeatures(product, request.ProductFeatures);

            // Mevcut görselleri yönet
            if (request.ExistingImageIds != null)
            {
                await RemoveUnusedImages(product, request.ExistingImageIds);
            }

            // Yeni görselleri ekle
            if (request.NewProductImages != null && request.NewProductImages.Any())
            {
                await AddNewImages(product, request);
            }

            // Vitrin görselini ayarla
            UpdateShowcaseImage(product, request.ShowcaseImageIndex);

            await _productRepository.UpdateAsync(product);

            var response = _mapper.Map<UpdatedProductResponse>(product);
            return response;
        }

        throw new Exception("Product not found");
    }

    private void UpdateBasicInformation(Product product, UpdateProductCommand request)
    {
        product.Name = request.Name;
        product.Title = request.Title;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.VaryantGroupID = request.VaryantGroupID;
        product.Tax = request.Tax;
        product.Stock = request.Stock;
        product.Price = request.Price;
        product.Sku = request.Sku;
    }

    private async Task UpdateProductFeatures(Product product, List<ProductFeatureDto> features)
    {
        product.ProductFeatureValues.Clear();
        foreach (var featureDto in features)
        {
            foreach (var featureValueDto in featureDto.FeatureValues)
            {
                product.ProductFeatureValues.Add(new ProductFeatureValue
                {
                    ProductId = product.Id,
                    FeatureValueId = featureValueDto.Id
                });
            }
        }
    }

    private async Task RemoveUnusedImages(Product product, List<string> existingImageIds)
    {
        var imagesToRemove = product.ProductImageFiles
            .Where(pif => !existingImageIds.Contains(pif.Id))
            .ToList();

        foreach (var imageToRemove in imagesToRemove)
        {
            product.ProductImageFiles.Remove(imageToRemove);
            await _imageFileRepository.DeleteAsync(imageToRemove);
            
            try
            {
                await _storageService.DeleteFromAllStoragesAsync(
                    "products", 
                    imageToRemove.Path, 
                    imageToRemove.Name);
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error deleting file from storages: {ex.Message}");
            }
        }
    }

    private async Task AddNewImages(Product product, UpdateProductCommand request)
    {
        for (int i = 0; i < request.NewProductImages.Count; i++)
        {
            var image = request.NewProductImages[i];
        
            // CreateMultiple'daki gibi doğrudan upload yapalım
            var uploadedFiles = await _storageService.UploadAsync(
                "products", 
                product.Id, 
                new List<IFormFile> { image });

            if (uploadedFiles.Any())
            {
                var uploadedFile = uploadedFiles.First();
                var productImageFile = new ProductImageFile(
                    uploadedFile.fileName,
                    uploadedFile.entityType,
                    uploadedFile.path,
                    uploadedFile.storageType)
                {
                    Alt = $"{product.Name} {i + 1}",
                    Title = product.Title,
                    Description = product.Description,
                    Format = uploadedFile.format,
                    // Diğer özellikleri ekleyin
                    Showcase = i == request.ShowcaseImageIndex
                };

                product.ProductImageFiles?.Add(productImageFile);
            }
        }
    }

    private void UpdateShowcaseImage(Product product, int? showcaseIndex)
    {
        if (showcaseIndex.HasValue && showcaseIndex.Value < product.ProductImageFiles?.Count)
        {
            foreach (var image in product.ProductImageFiles)
            {
                image.Showcase = false;
            }
            product.ProductImageFiles.ElementAt(showcaseIndex.Value).Showcase = true;
        }
    }
}
}