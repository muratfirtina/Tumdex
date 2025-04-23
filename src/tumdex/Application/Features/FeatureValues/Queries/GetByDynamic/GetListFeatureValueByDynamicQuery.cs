using System.Text.Json;
using Application.Consts;
using Application.Features.FeatureValues.Rules;
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

namespace Application.Features.FeatureValues.Queries.GetByDynamic;

public class GetListFeatureValueByDynamicQuery : IRequest<GetListResponse<GetListFeatureValueByDynamicDto>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }
    public string CacheKey => $"FeatureValues-Dynamic-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}-{JsonSerializer.Serialize(DynamicQuery)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.FeatureValues;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    
    public class GetListByDynamicFeatureValueQueryHandler : IRequestHandler<GetListFeatureValueByDynamicQuery, GetListResponse<GetListFeatureValueByDynamicDto>>
    {
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly IMapper _mapper;
        private readonly FeatureValueBusinessRules _featureValueBusinessRules;

        public GetListByDynamicFeatureValueQueryHandler(IFeatureValueRepository featureValueRepository, IMapper mapper, FeatureValueBusinessRules featureValueBusinessRules)
        {
            _featureValueRepository = featureValueRepository;
            _mapper = mapper;
            _featureValueBusinessRules = featureValueBusinessRules;
        }

        public async Task<GetListResponse<GetListFeatureValueByDynamicDto>> Handle(GetListFeatureValueByDynamicQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                var allFeatureValues = await _featureValueRepository.GetAllByDynamicAsync(
                    request.DynamicQuery,
                    cancellationToken: cancellationToken);

                var featureValuesDtos = _mapper.Map<GetListResponse<GetListFeatureValueByDynamicDto>>(allFeatureValues);
                return featureValuesDtos;
            }
            else
            {
                IPaginate<FeatureValue> featureValues = await _featureValueRepository.GetListByDynamicAsync(
                    request.DynamicQuery,
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken);
                
                var featureValuesDtos = _mapper.Map<GetListResponse<GetListFeatureValueByDynamicDto>>(featureValues);
                return featureValuesDtos;

            }
        }
    }
}