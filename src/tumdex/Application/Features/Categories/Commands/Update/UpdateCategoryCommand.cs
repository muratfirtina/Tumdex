using Application.Consts;
using Application.Features.Categories.Dtos;
using Application.Features.Categories.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Commands.Update;

public class UpdateCategoryCommand : IRequest<UpdatedCategoryResponse>,ITransactionalRequest, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<string>? SubCategoryIds { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<IFormFile>? NewCategoryImage { get; set; }
    public bool RemoveExistingImage { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, UpdatedCategoryResponse>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IFeatureRepository _featureRepository;
        private readonly IStorageService _storageService;
        private readonly IImageFileRepository _imageFileRepository;
        private readonly IMapper _mapper;

        public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository, IMapper mapper,
            CategoryBusinessRules categoryBusinessRules, IFeatureRepository featureRepository,
            IStorageService storageService, IImageFileRepository imageFileRepository)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _categoryBusinessRules = categoryBusinessRules;
            _featureRepository = featureRepository;
            _storageService = storageService;
            _imageFileRepository = imageFileRepository;
        }

        public async Task<UpdatedCategoryResponse> Handle(UpdateCategoryCommand request,
    CancellationToken cancellationToken)
{
    // Kategori var mı kontrolü
    Category? category = await _categoryRepository.GetAsync(p => p.Id == request.Id,
        include: c => c.Include(c => c.Features)
            .Include(c => c.CategoryImageFiles)
        , cancellationToken: cancellationToken);
    await _categoryBusinessRules.CategoryShouldExistWhenSelected(category);

    // Kategori adı benzersiz olmalı
    await _categoryBusinessRules.CategoryNameShouldBeUniqueWhenUpdate(request.Name, request.Id, cancellationToken);

    // Parent kategori ile ilgili iş kuralları
    await _categoryBusinessRules.ParentCategoryShouldNotBeSelf(request.Id, request.ParentCategoryId, cancellationToken);
    await _categoryBusinessRules.ParentCategoryShouldNotBeChild(request.Id, request.ParentCategoryId, cancellationToken);

    if (request.ParentCategoryId != null)
    {
        await _categoryBusinessRules.ParentCategoryShouldNotBeDescendant(request.Id, request.ParentCategoryId, cancellationToken);

        var parentCategory = await _categoryRepository.GetAsync(c => c.Id == request.ParentCategoryId);
        if (request.ParentCategoryId == null)
        {
            await _categoryBusinessRules.ParentCategoryShouldBeNullWhenUpdate(request.Id, request.ParentCategoryId, cancellationToken);
        }

        category.ParentCategory = parentCategory;
        category.ParentCategoryId = request.ParentCategoryId;
    }
    else
    {
        // ParentCategoryId null olduğunda, kategoriyi en üst seviye kategori yap
        if (category != null)
        {
            category.ParentCategory = null;
            category.ParentCategoryId = null;
        }
    }

    if (request.FeatureIds != null)
    {
        category?.Features?.Clear(); // Mevcut özellikleri temizle
        foreach (var featureId in request.FeatureIds)
        {
            var feature = await _featureRepository.GetAsync(feature => feature.Id == featureId);
            if (feature == null)
            {
                throw new Exception($"Feature with id {featureId} not found");
            }

            category?.Features?.Add(feature);
        }
    }
    else
    {
        category?.Features?.Clear(); // FeatureIds null ise tüm özellikleri kaldır
    }

    category.Name = request.Name ?? category.Name;
    category.Title = request.Title ?? category.Title;

    // Eğer yeni bir fotoğraf yüklendiyse eski fotoğrafı sil
    if (request.RemoveExistingImage)
    {
        var existingImage = category.CategoryImageFiles?.FirstOrDefault();
        if (existingImage != null)
        {
            category.CategoryImageFiles?.Remove(existingImage);
            await _imageFileRepository.DeleteAsync(existingImage);
            await _storageService.DeleteFromAllStoragesAsync("categories", existingImage.Path, existingImage.Name);
        }
    }

    if (request.NewCategoryImage != null && request.NewCategoryImage.Any())
    {
        // Eğer mevcut resim varsa ve yeni resim yükleniyorsa, mevcut resmi sil
        if (category.CategoryImageFiles != null && category.CategoryImageFiles.Count != 0)
        {
            var existingImage = category.CategoryImageFiles.First();
            category.CategoryImageFiles.Remove(existingImage);
            await _imageFileRepository.DeleteAsync(existingImage);
            await _storageService.DeleteFromAllStoragesAsync("categories", existingImage.Path, existingImage.Name);
        }

        var uploadedImage = await _storageService.UploadAsync("categories", category.Id, request.NewCategoryImage);
        foreach (var file in uploadedImage)
        {
            var categoryImageFile = new CategoryImageFile(file.fileName, file.entityType, file.path, file.storageType)
            {
                Format = file.format
            };
            category.CategoryImageFiles?.Add(categoryImageFile);
        }
    }

    await _categoryRepository.UpdateAsync(category);

    UpdatedCategoryResponse response = _mapper.Map<UpdatedCategoryResponse>(category);
    return response;
}

    }
}