using Application.Consts;
using Application.Features.FeatureValues.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.FeatureValues.Commands.Update;

public class UpdateFeatureValueCommand : IRequest<UpdatedFeatureValueResponse>,ITransactionalRequest,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string FeatureId { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;

    public class UpdateFeatureValueCommandHandler : IRequestHandler<UpdateFeatureValueCommand, UpdatedFeatureValueResponse>
    {
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly FeatureValueBusinessRules _featureValueBusinessRules;
        private readonly IMapper _mapper;

        public UpdateFeatureValueCommandHandler(IFeatureValueRepository featureValueRepository, IMapper mapper, FeatureValueBusinessRules featureValueBusinessRules)
        {
            _featureValueRepository = featureValueRepository;
            _mapper = mapper;
            _featureValueBusinessRules = featureValueBusinessRules;
        }

        public async Task<UpdatedFeatureValueResponse> Handle(UpdateFeatureValueCommand request,
            CancellationToken cancellationToken)
        {
            FeatureValue? featureValue = await _featureValueRepository.GetAsync(p => p.Id == request.Id,
                cancellationToken: cancellationToken);
            await _featureValueBusinessRules.FeatureValueShouldExistWhenSelected(featureValue);
            if (featureValue != null)
            {
                featureValue = _mapper.Map(request, featureValue);
                await _featureValueRepository.UpdateAsync(featureValue);
                UpdatedFeatureValueResponse response = _mapper.Map<UpdatedFeatureValueResponse>(featureValue);
                return response;
            }
            throw new Exception("FeatureValue not found");
        }
    }
}