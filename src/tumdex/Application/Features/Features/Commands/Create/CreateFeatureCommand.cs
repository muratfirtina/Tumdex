using Application.Consts;
using Application.Features.Features.Dtos;
using Application.Features.Features.Rules;
using Application.Features.FeatureValues.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.CrossCuttingConcerns.Exceptions;
using Core.Persistence.Repositories.Operation;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.Features.Commands.Create;

public class CreateFeatureCommand : IRequest<CreatedFeatureResponse>, ICacheRemoverRequest
{
    public string Name { get; set; }
    public List<string>? CategoryIds { get; set; }
    public List<FeatureValueCreateDto>? FeatureValues { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;

    public class CreateFeatureCommandHandler : IRequestHandler<CreateFeatureCommand, CreatedFeatureResponse>
    {
        private readonly IMapper _mapper;
        private readonly IFeatureRepository _featureRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly FeatureBusinessRules _featureBusinessRules;

        public CreateFeatureCommandHandler(IMapper mapper, IFeatureRepository featureRepository,
            ICategoryRepository categoryRepository, FeatureBusinessRules featureBusinessRules)
        {
            _mapper = mapper;
            _featureRepository = featureRepository;
            _categoryRepository = categoryRepository;
            _featureBusinessRules = featureBusinessRules;
        }

        public async Task<CreatedFeatureResponse> Handle(CreateFeatureCommand request,
            CancellationToken cancellationToken)
        {
            await _featureBusinessRules.FeatureNameShouldBeUniqueWhenCreate(request.Name, cancellationToken);
            await _featureBusinessRules.FeatureNameShouldNotBeNullOrEmpty(request.Name, cancellationToken);
            
            var feature = _mapper.Map<Feature>(request);
            
            if (request.CategoryIds != null)
            {
                ICollection<Category> categories = new List<Category>();
                foreach (var categoryId in request.CategoryIds)
                {
                    var category = await _categoryRepository.GetAsync(c => c.Id == categoryId);
                    if (category == null)
                    {
                        throw new BusinessException("Category not found");
                    }
                    categories.Add(category);
                }
                feature.Categories = categories;
            }
            
            if (request.FeatureValues != null)
            {
                var validFeatureValues = request.FeatureValues
                    .Where(fv => !string.IsNullOrWhiteSpace(fv.Name))
                    .ToList();
                
                foreach (var featureValueDto in validFeatureValues)
                {
                    string cleanedname = NameOperation.CharacterRegulatory(featureValueDto.Name.ToLower());
                    var randomPart = Guid.NewGuid().ToString("N").Substring(0, 16);
                    featureValueDto.Id = $"{cleanedname}-{randomPart}";
                }

                feature.FeatureValues = validFeatureValues.Select(dto => new FeatureValue
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    FeatureId = feature.Id
                }).ToList();
            }

            await _featureRepository.AddAsync(feature);
            CreatedFeatureResponse response = _mapper.Map<CreatedFeatureResponse>(feature);
            return response;
        }
    }
}
