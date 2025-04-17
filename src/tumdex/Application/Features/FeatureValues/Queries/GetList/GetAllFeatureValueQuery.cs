using Application.Consts;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;

namespace Application.Features.FeatureValues.Queries.GetList;

public class GetAllFeatureValueQuery : IRequest<GetListResponse<GetAllFeatureValueQueryResponse>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public string CacheKey => $"GetListFeatureValueQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.FeatureValues;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetAllFeatureValueQueryHandler : IRequestHandler<GetAllFeatureValueQuery, GetListResponse<GetAllFeatureValueQueryResponse>>
    {
        private readonly IFeatureValueRepository _featureValueRepository;
        private readonly IMapper _mapper;

        public GetAllFeatureValueQueryHandler(IFeatureValueRepository featureValueRepository, IMapper mapper)
        {
            _featureValueRepository = featureValueRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetAllFeatureValueQueryResponse>> Handle(GetAllFeatureValueQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                List<FeatureValue> featureValues = await _featureValueRepository.GetAllAsync(
                    orderBy: x => x.OrderBy(x => x.CreatedDate),
                    cancellationToken: cancellationToken);
                GetListResponse<GetAllFeatureValueQueryResponse> response = _mapper.Map<GetListResponse<GetAllFeatureValueQueryResponse>>(featureValues);
                return response;
            }
            else
            {
                IPaginate<FeatureValue> featureValues = await _featureValueRepository.GetListAsync(
                    orderBy: x => x.OrderBy(x => x.CreatedDate),
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken
                );
                GetListResponse<GetAllFeatureValueQueryResponse> response = _mapper.Map<GetListResponse<GetAllFeatureValueQueryResponse>>(featureValues);
                return response;
            }
        }
    }
}