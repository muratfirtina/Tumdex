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

namespace Application.Features.Categories.Commands.Create;

public class CreateCategoryCommand : IRequest<CreatedCategoryResponse>, ITransactionalRequest,ICacheRemoverRequest
{
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<IFormFile>? CategoryImage { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CreatedCategoryResponse>
    {
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeatureRepository _featureRepository;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IStorageService _storageService;

        public CreateCategoryCommandHandler(IMapper mapper, ICategoryRepository categoryRepository, IFeatureRepository featureRepository, CategoryBusinessRules categoryBusinessRules, IStorageService storageService)
        {
            _mapper = mapper;
            _categoryRepository = categoryRepository;
            _featureRepository = featureRepository;
            _categoryBusinessRules = categoryBusinessRules;
            _storageService = storageService;
        }

        public async Task<CreatedCategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            
            await _categoryBusinessRules.CategoryNameShouldBeUniqueWhenCreate(request.Name, cancellationToken);
    
            var category = _mapper.Map<Category>(request);

            if (request.ParentCategoryId != null)
            {
                await _categoryBusinessRules.CategoryIdShouldExistWhenSelected(request.ParentCategoryId, cancellationToken);
                var parentCategory = await _categoryRepository.GetAsync(category => category.Id == request.ParentCategoryId);
                category.ParentCategory = parentCategory;
            }

            if (request.FeatureIds != null)
            {
                ICollection<Feature>? features = new List<Feature>();
                foreach (var featureId in request.FeatureIds)
                {
                    var feature = await _featureRepository.GetAsync(feature => feature.Id == featureId);
                    if (feature == null)
                    {
                        throw new BusinessException("Feature not found");
                    }
                    features.Add(feature);
                }
                category.Features = features;
            }

            await _categoryRepository.AddAsync(category);
            
            if (request.CategoryImage != null)
            {
                var uploadResult = await _storageService.UploadAsync("categories", category.Id, request.CategoryImage);
                if (uploadResult.Any())
                {
                    var (fileName, path, _, storageType,url,format) = uploadResult.First();
                    var categoryImageFile = new CategoryImageFile(fileName, "categories", path, storageType)
                    {
                        Format = format
                    };
                    category.CategoryImageFiles = new List<CategoryImageFile> { categoryImageFile };
                    await _categoryRepository.UpdateAsync(category);
                }
            }
            CreatedCategoryResponse response = _mapper.Map<CreatedCategoryResponse>(category);
            return response;
        }

    }
    
}