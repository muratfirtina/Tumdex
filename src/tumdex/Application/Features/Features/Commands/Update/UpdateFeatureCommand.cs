using Application.Consts;
using Application.Features.Features.Dtos;
using Application.Features.Features.Rules;
using Application.Features.FeatureValues.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Core.CrossCuttingConcerns.Exceptions;
using Core.Persistence.Repositories.Operation;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Features.Commands.Update
{
    public class UpdateFeatureCommand : IRequest<UpdatedFeatureResponse>, ITransactionalRequest, ICacheRemoverRequest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string>? CategoryIds { get; set; } = new();
        public List<string>? FeatureValueIds { get; set; } = new();
        public List<FeatureValueCreateDto>? FeatureValues { get; set; } = new();
        public string CacheKey => "";
        public bool BypassCache => false;
        public string? CacheGroupKey => $"{CacheGroups.Features},{CacheGroups.FeatureValues},{CacheGroups.GetAll}";
    }

    public class UpdateFeatureCommandHandler : IRequestHandler<UpdateFeatureCommand, UpdatedFeatureResponse>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly FeatureBusinessRules _featureBusinessRules;
        private readonly IMapper _mapper;

        public UpdateFeatureCommandHandler(
            IFeatureRepository featureRepository,
            ICategoryRepository categoryRepository,
            IFeatureValueRepository featureValueRepository,
            IMapper mapper,
            FeatureBusinessRules featureBusinessRules)
        {
            _featureRepository = featureRepository;
            _categoryRepository = categoryRepository;
            _featureValueRepository = featureValueRepository;
            _mapper = mapper;
            _featureBusinessRules = featureBusinessRules;
        }

        public async Task<UpdatedFeatureResponse> Handle(UpdateFeatureCommand request, CancellationToken cancellationToken)
        {
            Feature? feature = await _featureRepository.GetAsync(
                include: f => f.Include(f => f.FeatureValues).Include(f => f.Categories),
                predicate: p => p.Id == request.Id,
                cancellationToken: cancellationToken);

            await _featureBusinessRules.FeatureShouldExistWhenSelected(feature);

            if (feature != null)
            {
                await _featureBusinessRules.FeatureNameShouldBeUniqueWhenUpdate(request.Name, request.Id, cancellationToken);
                feature.Name = request.Name;

                // Match categories
                var categories = await _categoryRepository.GetAllAsync(c => request.CategoryIds.Contains(c.Id));
                feature.Categories = categories.ToList();

                // Determine new feature value IDs
                var existingFeatureValueIds = feature.FeatureValues.Select(fv => fv.Id).ToList();
                var newFeatureValueIds = request.FeatureValueIds.Except(existingFeatureValueIds).ToList();

                // Check only new feature value IDs
                await _featureBusinessRules.FeatureValueIdShouldNotExistWhenSelected(newFeatureValueIds, cancellationToken);

                // Assign existing feature values
                var featureValues = await _featureValueRepository.GetAllAsync(fv => request.FeatureValueIds.Contains(fv.Id));
                feature.FeatureValues = featureValues.ToList();

                // Add new feature values from DTO list if any
                if (request.FeatureValues != null && request.FeatureValues.Count > 0)
                {
                    var validFeatureValues = request.FeatureValues
                        .Where(fv => !string.IsNullOrWhiteSpace(fv.Name))
                        .ToList();

                    foreach (var featureValueDto in validFeatureValues)
                    {
                        string cleanedname = NameOperation.CharacterRegulatory(featureValueDto.Name.ToLower());
                        var randomPart = Guid.NewGuid().ToString("N").Substring(0, 16);
                        var newId = $"{cleanedname}-{randomPart}";

                        var newFeatureValue = new FeatureValue
                        {
                            Id = newId,
                            Name = featureValueDto.Name,
                            FeatureId = feature.Id
                        };

                        feature.FeatureValues.Add(newFeatureValue);
                    }
                }

                await _featureRepository.UpdateAsync(feature);
                UpdatedFeatureResponse response = _mapper.Map<UpdatedFeatureResponse>(feature);
                return response;
            }

            throw new BusinessException("Feature not found");
        }
    }
}