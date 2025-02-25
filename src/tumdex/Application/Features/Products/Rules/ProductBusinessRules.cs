using Application.Features.Brands.Rules;
using Application.Features.Categories.Rules;
using Application.Features.Features.Rules;
using Application.Features.FeatureValues.Rules;
using Application.Features.Products.Consts;
using Application.Repositories;
using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;

namespace Application.Features.Products.Rules;

public class ProductBusinessRules : BaseBusinessRules
{
    private readonly IProductRepository _productRepository;
    private readonly BrandBusinessRules _bransBusinessRules;
    private readonly CategoryBusinessRules _categoryBusinessRules;
    private readonly FeatureBusinessRules _featureBusinessRules;
    private readonly FeatureValueBusinessRules _featureValueBusinessRules;

    public ProductBusinessRules(IProductRepository productRepository, BrandBusinessRules bransBusinessRules, CategoryBusinessRules categoryBusinessRules, FeatureBusinessRules featureBusinessRules, FeatureValueBusinessRules featureValueBusinessRules)
    {
        _productRepository = productRepository;
        _bransBusinessRules = bransBusinessRules;
        _categoryBusinessRules = categoryBusinessRules;
        _featureBusinessRules = featureBusinessRules;
        _featureValueBusinessRules = featureValueBusinessRules;
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
        if (images.Count(i => i.Showcase) > 1)
            throw new BusinessException("Bir üründe sadece bir vitrin fotoğrafı olabilir.");
        return Task.CompletedTask;
    }
}