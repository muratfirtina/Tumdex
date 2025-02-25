using Application.Features.FeatureValues.Consts;
using Application.Repositories;
using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;

namespace Application.Features.FeatureValues.Rules;

public class FeatureValueBusinessRules : BaseBusinessRules
{
    private readonly IFeatureValueRepository _featureRepository;

    public FeatureValueBusinessRules(IFeatureValueRepository featureRepository)
    {
        _featureRepository = featureRepository;
    }

    public Task FeatureValueShouldExistWhenSelected(FeatureValue? feature)
    {
        if (feature == null)
            throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNotExists);
        return Task.CompletedTask;
    }

    public async Task FeatureValueIdShouldExistWhenSelected(string id, CancellationToken cancellationToken)
    {
        FeatureValue? feature = await _featureRepository.GetAsync(
            predicate: e => e.Id == id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        await FeatureValueShouldExistWhenSelected(feature);
    }
    
    public async Task FeatureValueNameShouldBeUniqueWhenUpdate(string name, string id, CancellationToken cancellationToken)
    {
        FeatureValue? feature = await _featureRepository.GetAsync(
            predicate: e => e.Name == name && e.Id != id,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        if (feature != null)
            throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNameAlreadyExists);
    }
    
    public async Task FeatureValueNameShouldBeUniqueWhenCreate(string name, CancellationToken cancellationToken)
    {
        FeatureValue? feature = await _featureRepository.GetAsync(
            predicate: e => e.Name == name,
            enableTracking: false,
            cancellationToken: cancellationToken
        );
        if (feature != null)
            throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNameAlreadyExists);
    }
    
    // featurevalue name boş olamaz, null olamaz ve boşluk ile başlayamaz
    public Task FeatureValueNameShouldNotBeNullOrEmpty(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNameShouldNotBeNullOrEmpty);
        if (string.IsNullOrEmpty(name))
            throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNameShouldNotBeNullOrEmpty);
        if (name.Contains(" "))
            throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNameShouldNotBeNullOrEmpty);
        return Task.CompletedTask;
        throw new BusinessException(FeatureValuesBusinessMessages.FeatureValueNameShouldNotBeNullOrEmpty);
    }
    
    
}