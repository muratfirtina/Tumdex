using Application.Features.Categories.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Core.CrossCuttingConcerns.Exceptions;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categories.Commands.Create;

public class CreateCategoryCommand : IRequest<CreatedCategoryResponse>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<IFormFile>? CategoryImage { get; set; }

    // ICacheRemoverRequest implementation
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;
    public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CreatedCategoryResponse>
    {
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeatureRepository _featureRepository;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IStorageService _storageService;
        private readonly ILogger<CreateCategoryCommandHandler> _logger;

        public CreateCategoryCommandHandler(
            IMapper mapper,
            ICategoryRepository categoryRepository,
            IFeatureRepository featureRepository,
            CategoryBusinessRules categoryBusinessRules,
            IStorageService storageService,
            ILogger<CreateCategoryCommandHandler> logger)
        {
            _mapper = mapper;
            _categoryRepository = categoryRepository;
            _featureRepository = featureRepository;
            _categoryBusinessRules = categoryBusinessRules;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<CreatedCategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Attempting to create category with Name: {CategoryName}", request.Name);
            await _categoryBusinessRules.CategoryNameShouldBeUniqueWhenCreate(request.Name, cancellationToken);

            var category = _mapper.Map<Category>(request);

            // Parent Kategori İlişkisi
            if (!string.IsNullOrWhiteSpace(request.ParentCategoryId))
            {
                await _categoryBusinessRules.CategoryIdShouldExistWhenSelected(request.ParentCategoryId, cancellationToken);
                var parentCategory = await _categoryRepository.GetAsync(c => c.Id == request.ParentCategoryId, cancellationToken: cancellationToken);
                // İş kuralı null kontrolü yapmalı, ama ek kontrol
                 if (parentCategory == null) {
                     _logger.LogError("Parent category not found with ID: {ParentCategoryId}", request.ParentCategoryId);
                     throw new BusinessException($"Parent category with ID '{request.ParentCategoryId}' not found.");
                 }
                category.ParentCategory = parentCategory;
                _logger.LogDebug("Parent category {ParentCategoryId} assigned to new category.", request.ParentCategoryId);
            }

            // Feature İlişkisi
            if (request.FeatureIds != null && request.FeatureIds.Any())
            {
                _logger.LogDebug("Assigning {FeatureCount} features to the new category.", request.FeatureIds.Count);
                ICollection<Feature> features = new List<Feature>();
                foreach (var featureId in request.FeatureIds)
                {
                    var feature = await _featureRepository.GetAsync(f => f.Id == featureId, cancellationToken: cancellationToken);
                    if (feature == null)
                    {
                        _logger.LogError("Feature not found with ID: {FeatureId}", featureId);
                        throw new BusinessException($"Feature with ID '{featureId}' not found.");
                    }
                    features.Add(feature);
                }
                category.Features = features;
            }

             // Kategori Ekleme
            await _categoryRepository.AddAsync(category);
             _logger.LogInformation("Category entity added to context with ID: {CategoryId}", category.Id);

            // Resim Yükleme ve Kaydetme
            if (request.CategoryImage != null && request.CategoryImage.Any())
            {
                 _logger.LogInformation("Uploading category image for category: {CategoryName}", request.Name);
                 var uploadResult = await _storageService.UploadAsync("categories", category.Id, request.CategoryImage);
                 if (uploadResult.Any())
                 {
                     var (fileName, path, _, storageType, url, format) = uploadResult.First();
                     var categoryImageFile = new CategoryImageFile(fileName, "categories", path, storageType)
                     {
                         Format = format
                     };
                     
                     category.CategoryImageFiles = new List<CategoryImageFile> { categoryImageFile };
                     await _categoryRepository.UpdateAsync(category);
                     _logger.LogInformation("CategoryImageFile entity associated with CategoryId: {CategoryId}", category.Id);
                 }
            }

            CreatedCategoryResponse response = _mapper.Map<CreatedCategoryResponse>(category);
             _logger.LogInformation("Category created successfully: {CategoryId}, Name: {CategoryName}", category.Id, category.Name);
            return response;
        }
    }
}