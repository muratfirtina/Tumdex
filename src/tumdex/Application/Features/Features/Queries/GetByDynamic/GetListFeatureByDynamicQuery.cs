using Application.Consts;
using Application.Features.Features.Rules;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Features.Queries.GetByDynamic;

public class GetListFeatureByDynamicQuery : IRequest<GetListResponse<GetListFeatureByDynamicDto>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }
    public string CacheKey => $"GetListFeatureByDynamicQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.Features;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    
    public class GetListByDynamicFeatureQueryHandler : IRequestHandler<GetListFeatureByDynamicQuery, GetListResponse<GetListFeatureByDynamicDto>>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly IMapper _mapper;
        private readonly FeatureBusinessRules _featureBusinessRules;

        public GetListByDynamicFeatureQueryHandler(IFeatureRepository featureRepository, IMapper mapper, FeatureBusinessRules featureBusinessRules)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
            _featureBusinessRules = featureBusinessRules;
        }

        public async Task<GetListResponse<GetListFeatureByDynamicDto>> Handle(GetListFeatureByDynamicQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                var allFeatures = await _featureRepository.GetAllByDynamicAsync(
                    request.DynamicQuery,
                    cancellationToken: cancellationToken);

                var featuresDtos = _mapper.Map<GetListResponse<GetListFeatureByDynamicDto>>(allFeatures);
                return featuresDtos;
            }
            else
            {
                IPaginate<Feature> features = await _featureRepository.GetListByDynamicAsync(
                    request.DynamicQuery,
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken);
                
                var featuresDtos = _mapper.Map<GetListResponse<GetListFeatureByDynamicDto>>(features);
                return featuresDtos;

            }
        }
    }
}