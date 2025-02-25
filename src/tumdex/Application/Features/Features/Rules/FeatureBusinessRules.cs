using Application.Features.Features.Consts;
using Application.Repositories;
using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;

namespace Application.Features.Features.Rules;

public class FeatureBusinessRules : BaseBusinessRules
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IFeatureValueRepository _featureValueRepository;

    public FeatureBusinessRules(IFeatureRepository featureRepository, IFeatureValueRepository featureValueRepository)
    {
        _featureRepository = featureRepository;
        _featureValueRepository = featureValueRepository;
    }

    public Task FeatureShouldExistWhenSelected(Feature? feature)
    {
        if (feature == null)
            throw new BusinessException(FeaturesBusinessMessages.FeatureNotExists);
        return Task.CompletedTask;
    }

    public async Task FeatureIdShouldExistWhenSelected(string id, CancellationToken cancellationToken)
    {
        Feature? feature = await _featureRepository.GetAsync(
            predicate: e => e.Id == id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        await FeatureShouldExistWhenSelected(feature);
    }

    public async Task FeatureNameShouldBeUniqueWhenUpdate(string name, string id, CancellationToken cancellationToken)
    {
        Feature? feature = await _featureRepository.GetAsync(
            predicate: e => e.Name == name && e.Id != id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        if (feature != null)
            throw new BusinessException(FeaturesBusinessMessages.FeatureNameAlreadyExists);
    }
    
    public async Task FeatureNameShouldBeUniqueWhenCreate(string name, CancellationToken cancellationToken)
    {
        Feature? feature = await _featureRepository.GetAsync(
            predicate: e => e.Name == name,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        if (feature != null)
            throw new BusinessException(FeaturesBusinessMessages.FeatureNameAlreadyExists);
    }

    public async Task FeatureNameShouldNotBeNullOrEmpty(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
            throw new BusinessException(FeaturesBusinessMessages.FeatureNameCannotBeEmpty);
    }
    
    //gelen FeatureValueIds eğer başka bir feature a aitse hata verir. 
    public async Task FeatureValueIdShouldNotExistWhenSelected(List<string>? featureValueIds, CancellationToken cancellationToken)
    {
        foreach (var featureValueId in featureValueIds)
        {
            FeatureValue? featureValue = await _featureValueRepository.GetAsync(
                predicate: e => e.Id == featureValueId,
                enableTracking: false,
                cancellationToken: cancellationToken
            );
            if (featureValue != null)
                throw new BusinessException(FeaturesBusinessMessages.FeatureValueIdAlreadyExists);
        }
    }
    
}