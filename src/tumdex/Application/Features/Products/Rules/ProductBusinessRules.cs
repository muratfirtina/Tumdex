using Application.Features.Brands.Rules;
using Application.Features.Categories.Rules;
using Application.Features.Features.Rules;
using Application.Features.FeatureValues.Rules;
using Application.Features.Products.Consts;
using Application.Repositories;
using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Rules;

public class ProductBusinessRules : BaseBusinessRules
{
    private readonly IProductRepository _productRepository;
    private readonly BrandBusinessRules _bransBusinessRules;
    private readonly CategoryBusinessRules _categoryBusinessRules;
    private readonly FeatureBusinessRules _featureBusinessRules;
    private readonly FeatureValueBusinessRules _featureValueBusinessRules;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IFeatureValueRepository _featureValueRepository;
    private readonly ILogger<ProductBusinessRules> _logger;

    public ProductBusinessRules(
        IProductRepository productRepository, 
        BrandBusinessRules bransBusinessRules, 
        CategoryBusinessRules categoryBusinessRules, 
        FeatureBusinessRules featureBusinessRules, 
        FeatureValueBusinessRules featureValueBusinessRules,
        ICategoryRepository categoryRepository,
        IBrandRepository brandRepository,
        IFeatureValueRepository featureValueRepository,
        ILogger<ProductBusinessRules> logger)
    {
        _productRepository = productRepository;
        _bransBusinessRules = bransBusinessRules;
        _categoryBusinessRules = categoryBusinessRules;
        _featureBusinessRules = featureBusinessRules;
        _featureValueBusinessRules = featureValueBusinessRules;
        _categoryRepository = categoryRepository;
        _brandRepository = brandRepository;
        _featureValueRepository = featureValueRepository;
        _logger = logger;
    }

    public Task ProductShouldExistWhenSelected(Product? product)
    {
        if (product == null)
            throw new BusinessException(ProductsBusinessMessages.ProductNotExists);
        return Task.CompletedTask;
    }

    public async Task ProductIdShouldExistWhenSelected(string id, CancellationToken cancellationToken)
    {
        Product? product = await _productRepository.GetAsync(
            predicate: e => e.Id == id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        await ProductShouldExistWhenSelected(product);
    }
    
    public async Task CheckBrandAndCategoryExist(string brandId, string categoryId)
    {
        await _bransBusinessRules.BrandIdShouldExistWhenSelected(brandId, cancellationToken: default);
        await _categoryBusinessRules.CategoryIdShouldExistWhenSelected(categoryId, cancellationToken: default);
    }
    
    public async Task FeatureIdShouldExistWhenSelected(string id, CancellationToken cancellationToken)
    {
        await _featureBusinessRules.FeatureIdShouldExistWhenSelected(id, cancellationToken);
    }
    
    public async Task FeatureValueIdShouldExistWhenSelected(string id, CancellationToken cancellationToken)
    {
        await _featureValueBusinessRules.FeatureValueIdShouldExistWhenSelected(id, cancellationToken);
    }
    
    public Task EnsureOnlyOneShowcaseImage(ICollection<ProductImageFile>? images)
    {
        if (images == null)
            return Task.CompletedTask;
            
        if (images.Count(i => i.Showcase) > 1)
            throw new BusinessException("Bir üründe sadece bir vitrin fotoğrafı olabilir.");
        return Task.CompletedTask;
    }

    // Toplu kategori varlık kontrolü
    public async Task CategoriesShouldExistWhenSelected(List<string> categoryIds, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking existence of {Count} categories", categoryIds.Count);
        
        if (!categoryIds.Any())
            return;
            
        var categories = await _categoryRepository.GetListAsync(
            predicate: c => categoryIds.Contains(c.Id),
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        
        if (categories.Count != categoryIds.Count)
        {
            var existingIds = categories.Items.Select(c => c.Id).ToList();
            var missingIds = categoryIds.Except(existingIds).ToList();
            
            _logger.LogWarning("Categories not found: {MissingIds}", string.Join(", ", missingIds));
            throw new BusinessException($"Bu kategoriler bulunamadı: {string.Join(", ", missingIds)}");
        }
        
        _logger.LogInformation("All {Count} categories exist", categoryIds.Count);
    }
    
    // Toplu marka varlık kontrolü
    public async Task BrandsShouldExistWhenSelected(List<string> brandIds, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking existence of {Count} brands", brandIds.Count);
        
        if (!brandIds.Any())
            return;
            
        var brands = await _brandRepository.GetListAsync(
            predicate: b => brandIds.Contains(b.Id),
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        
        if (brands.Count != brandIds.Count)
        {
            var existingIds = brands.Items.Select(b => b.Id).ToList();
            var missingIds = brandIds.Except(existingIds).ToList();
            
            _logger.LogWarning("Brands not found: {MissingIds}", string.Join(", ", missingIds));
            throw new BusinessException($"Bu markalar bulunamadı: {string.Join(", ", missingIds)}");
        }
        
        _logger.LogInformation("All {Count} brands exist", brandIds.Count);
    }
    
    // Toplu özellik değeri varlık kontrolü
    public async Task FeatureValuesShouldExistWhenSelected(List<string> featureValueIds, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking existence of {Count} feature values", featureValueIds.Count);
        
        if (!featureValueIds.Any())
            return;
            
        var featureValues = await _featureValueRepository.GetListAsync(
            predicate: fv => featureValueIds.Contains(fv.Id),
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        
        if (featureValues.Count != featureValueIds.Count)
        {
            var existingIds = featureValues.Items.Select(fv => fv.Id).ToList();
            var missingIds = featureValueIds.Except(existingIds).ToList();
            
            _logger.LogWarning("Feature values not found: {MissingIds}", string.Join(", ", missingIds));
            throw new BusinessException($"Bu özellik değerleri bulunamadı: {string.Join(", ", missingIds)}");
        }
        
        _logger.LogInformation("All {Count} feature values exist", featureValueIds.Count);
    }
    public async Task CategoryShouldExistWhenSelected(string categoryId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking existence of category with ID: {CategoryId}", categoryId);
    
        if (string.IsNullOrEmpty(categoryId))
            throw new BusinessException("Kategori ID'si boş olamaz.");
        
        var category = await _categoryRepository.GetAsync(
            predicate: c => c.Id == categoryId,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
    
        if (category == null)
        {
            _logger.LogWarning("Category not found with ID: {CategoryId}", categoryId);
            throw new BusinessException($"ID'si {categoryId} olan kategori bulunamadı.");
        }
    
        _logger.LogInformation("Category exists with ID: {CategoryId}", categoryId);
    }

// Tekil marka varlık kontrolü
    public async Task BrandShouldExistWhenSelected(string brandId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking existence of brand with ID: {BrandId}", brandId);
    
        if (string.IsNullOrEmpty(brandId))
            throw new BusinessException("Marka ID'si boş olamaz.");
        
        var brand = await _brandRepository.GetAsync(
            predicate: b => b.Id == brandId,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
    
        if (brand == null)
        {
            _logger.LogWarning("Brand not found with ID: {BrandId}", brandId);
            throw new BusinessException($"ID'si {brandId} olan marka bulunamadı.");
        }
    
        _logger.LogInformation("Brand exists with ID: {BrandId}", brandId);
    }
}