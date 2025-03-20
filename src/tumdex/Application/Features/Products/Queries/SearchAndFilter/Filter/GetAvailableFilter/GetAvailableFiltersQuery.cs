using Application.Consts;
using Application.Features.Products.Dtos.FilterDto;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.Products.Queries.SearchAndFilter.Filter.GetAvailableFilter;

public class GetAvailableFiltersQuery : IRequest<List<FilterGroupDto>>, ICachableRequest
{
    public string SearchTerm { get; set; }
    
    // More descriptive cache key with search term
    public string CacheKey => $"Filters-{SearchTerm ?? "all"}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
}

public class GetAvailableFiltersQueryHandler : IRequestHandler<GetAvailableFiltersQuery, List<FilterGroupDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public GetAvailableFiltersQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<List<FilterGroupDto>> Handle(GetAvailableFiltersQuery request, CancellationToken cancellationToken)
    {
        var filters = await _productRepository.GetAvailableFilters(request.SearchTerm);
        return _mapper.Map<List<FilterGroupDto>>(filters);
    }
}