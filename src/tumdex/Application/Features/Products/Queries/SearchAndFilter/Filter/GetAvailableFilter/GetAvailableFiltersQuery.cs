using Application.Consts;
using Application.Features.Products.Dtos.FilterDto;
using Application.Repositories;
using MediatR;
using System.Text.Json;
using Core.Application.Pipelines.Caching;

namespace Application.Features.Products.Queries.SearchAndFilter.Filter.GetAvailableFilter;

public class GetAvailableFiltersQuery : IRequest<List<FilterGroupDto>>, ICachableRequest
{
    public string? SearchTerm { get; set; }
    public string[]? CategoryIds { get; set; }
    public string[]? BrandIds { get; set; }
    
    // Daha detaylı cache key
    public string CacheKey => $"Filters-{SearchTerm ?? "all"}-Categories-{SerializeIds(CategoryIds)}-Brands-{SerializeIds(BrandIds)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
    
    // ID'leri cache key için serileştiren yardımcı metod
    private string SerializeIds(string[] ids)
    {
        if (ids == null || ids.Length == 0)
            return "none";
            
        return string.Join("-", ids.OrderBy(id => id));
    }
}

public class GetAvailableFiltersQueryHandler : IRequestHandler<GetAvailableFiltersQuery, List<FilterGroupDto>>
{
    private readonly IProductRepository _productRepository;

    public GetAvailableFiltersQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<FilterGroupDto>> Handle(GetAvailableFiltersQuery request, CancellationToken cancellationToken)
    {
        // Artık doğrudan FilterGroupDto döndüren repository metodunu çağırıyoruz
        return await _productRepository.GetAvailableFilters(
            request.SearchTerm,
            request.CategoryIds,
            request.BrandIds
        );
    }
}