using Application.Consts;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Features.Queries.GetList;

public class GetAllFeatureQuery : IRequest<GetListResponse<GetAllFeatureQueryResponse>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public string CacheKey => $"GetListFeatureQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetAllFeatureQueryHandler : IRequestHandler<GetAllFeatureQuery, GetListResponse<GetAllFeatureQueryResponse>>
    {
        private readonly IFeatureRepository _featureRepository;
        private readonly IMapper _mapper;

        public GetAllFeatureQueryHandler(IFeatureRepository featureRepository, IMapper mapper)
        {
            _featureRepository = featureRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetAllFeatureQueryResponse>> Handle(GetAllFeatureQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                List<Feature> features = await _featureRepository.GetAllAsync(
                    include: x => x.Include(x => x.FeatureValues),
                    cancellationToken: cancellationToken);
                GetListResponse<GetAllFeatureQueryResponse> response = _mapper.Map<GetListResponse<GetAllFeatureQueryResponse>>(features);
                return response;
            }
            else
            {
                IPaginate<Feature> features = await _featureRepository.GetListAsync(
                    include: x => x.Include(x => x.FeatureValues),
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken
                );
                GetListResponse<GetAllFeatureQueryResponse> response = _mapper.Map<GetListResponse<GetAllFeatureQueryResponse>>(features);
                return response;
            }
        }
    }
}