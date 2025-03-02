using Application.Consts;
using Application.Features.Features.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.Features.Commands.Delete;

public class DeleteFeatureCommand : IRequest<DeletedFeatureResponse>, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    
    public class DeleteFeatureCommandHandler : IRequestHandler<DeleteFeatureCommand, DeletedFeatureResponse>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly FeatureBusinessRules _featureBusinessRules;
        private readonly IMapper _mapper;

        public DeleteFeatureCommandHandler(IFeatureRepository featureRepository, IMapper mapper, FeatureBusinessRules featureBusinessRules)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
            _featureBusinessRules = featureBusinessRules;
        }

        public async Task<DeletedFeatureResponse> Handle(DeleteFeatureCommand request, CancellationToken cancellationToken)
        {
            Feature? feature = await _featureRepository.GetAsync(p=>p.Id==request.Id,cancellationToken: cancellationToken);
            await _featureBusinessRules.FeatureShouldExistWhenSelected(feature);
            await _featureRepository.DeleteAsync(feature!);
            DeletedFeatureResponse response = _mapper.Map<DeletedFeatureResponse>(feature);
            response.Success = true;
            return response;
        }
    }
}