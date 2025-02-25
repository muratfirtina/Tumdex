using Application.Consts;
using Application.Features.FeatureValues.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using MediatR;

namespace Application.Features.FeatureValues.Commands.Delete;

public class DeleteFeatureValueCommand : IRequest<DeletedFeatureValueResponse>,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class DeleteFeatureValueCommandHandler : IRequestHandler<DeleteFeatureValueCommand, DeletedFeatureValueResponse>
    {
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly FeatureValueBusinessRules _featureValueBusinessRules;
        private readonly IMapper _mapper;

        public DeleteFeatureValueCommandHandler(IFeatureValueRepository featureValueRepository, IMapper mapper, FeatureValueBusinessRules featureValueBusinessRules)
        {
            _featureValueRepository = featureValueRepository;
            _mapper = mapper;
            _featureValueBusinessRules = featureValueBusinessRules;
        }

        public async Task<DeletedFeatureValueResponse> Handle(DeleteFeatureValueCommand request, CancellationToken cancellationToken)
        {
            FeatureValue? featureValue = await _featureValueRepository.GetAsync(p=>p.Id==request.Id,cancellationToken: cancellationToken);
            await _featureValueBusinessRules.FeatureValueShouldExistWhenSelected(featureValue);
            await _featureValueRepository.DeleteAsync(featureValue!);
            DeletedFeatureValueResponse response = _mapper.Map<DeletedFeatureValueResponse>(featureValue);
            response.Success = true;
            return response;
        }
    }
}