using Application.Consts;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.FeatureValues.Queries.GetById;

public class GetByIdFeatureValueQuery : IRequest<GetByIdFeatureValueResponse>,ICachableRequest
{
    public string Id { get; set; }
    public string CacheKey => $"GetByIdFeatureValueQuery({Id})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.FeatureValues;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetByIdFeatureValueQueryHandler : IRequestHandler<GetByIdFeatureValueQuery, GetByIdFeatureValueResponse>
    {
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly IMapper _mapper;

        public GetByIdFeatureValueQueryHandler(IFeatureValueRepository featureValueRepository, IMapper mapper)
        {
            _featureValueRepository = featureValueRepository;
            _mapper = mapper;
        }

        public async Task<GetByIdFeatureValueResponse> Handle(GetByIdFeatureValueQuery request, CancellationToken cancellationToken)
        {
            FeatureValue? featureValue = await _featureValueRepository.GetAsync(
                predicate: p => p.Id == request.Id,
                include: f => f.Include(f => f.Feature),
                cancellationToken: cancellationToken);
            GetByIdFeatureValueResponse response = _mapper.Map<GetByIdFeatureValueResponse>(featureValue);
            return response;
        }
    }
}