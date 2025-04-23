using Application.Consts;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Features.Queries.GetById;

public class GetByIdFeatureQuery : IRequest<GetByIdFeatureResponse>,ICachableRequest
{
    public string Id { get; set; }
    public string CacheKey => $"Feature-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Features;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1);
    
    public class GetByIdFeatureQueryHandler : IRequestHandler<GetByIdFeatureQuery, GetByIdFeatureResponse>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly IMapper _mapper;

        public GetByIdFeatureQueryHandler(IFeatureRepository featureRepository, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
        }

        public async Task<GetByIdFeatureResponse> Handle(GetByIdFeatureQuery request, CancellationToken cancellationToken)
        {
            Feature? feature = await _featureRepository.GetAsync(
                include: f => f.Include(f => f.FeatureValues).Include(f => f.Categories),
                predicate: p => p.Id == request.Id,
                cancellationToken: cancellationToken);
            GetByIdFeatureResponse response = _mapper.Map<GetByIdFeatureResponse>(feature);
            return response;
        }
    }
}