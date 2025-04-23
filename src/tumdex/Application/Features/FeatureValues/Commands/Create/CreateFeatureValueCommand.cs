using Application.Consts;
using Application.Features.FeatureValues.Commands.Create;
using Application.Features.FeatureValues.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.FeatureValues.Commands.Create;

public class CreateFeatureValueCommand : IRequest<CreatedFeatureValueResponse>,ICacheRemoverRequest
{
    public string Name { get; set; }
    public string FeatureId { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;
    
    public class CreateFeatureCommandHandler : IRequestHandler<CreateFeatureValueCommand, CreatedFeatureValueResponse>
    {
        private readonly IMapper _mapper;
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly FeatureValueBusinessRules _featureValueBusinessRules;

        public CreateFeatureCommandHandler(IMapper mapper, IFeatureValueRepository featureValueRepository, FeatureValueBusinessRules featureValueBusinessRules)
        {
            _mapper = mapper;
            _featureValueRepository = featureValueRepository;
            _featureValueBusinessRules = featureValueBusinessRules;
        }

        public async Task<CreatedFeatureValueResponse> Handle(CreateFeatureValueCommand request, CancellationToken cancellationToken)
        {
            await _featureValueBusinessRules.FeatureValueNameShouldBeUniqueWhenCreate(request.Name, cancellationToken);
            await _featureValueBusinessRules.FeatureValueNameShouldNotBeNullOrEmpty(request.Name, cancellationToken);
            var featureValue = _mapper.Map<FeatureValue>(request);
            await _featureValueRepository.AddAsync(featureValue);
            
            CreatedFeatureValueResponse response = _mapper.Map<CreatedFeatureValueResponse>(featureValue);
            return response;
        }
    }
    
}