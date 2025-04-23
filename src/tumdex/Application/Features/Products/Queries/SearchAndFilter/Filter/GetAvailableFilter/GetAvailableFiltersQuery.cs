using Application.Consts;
using Application.Features.Products.Dtos.FilterDto;
using Application.Repositories;
using MediatR;
using System.Text.Json;
using Core.Application.Pipelines.Caching;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.SearchAndFilter.Filter.GetAvailableFilter;

public class GetAvailableFiltersQuery : IRequest<List<FilterGroupDto>>, ICachableRequest
{
    public string? SearchTerm { get; set; }
    public string[]? CategoryIds { get; set; }
    public string[]? BrandIds { get; set; }

    // ICachableRequest implementation
    // Deterministik cache key için ID'leri sırala
    private string SerializeIds(string[]? ids) => ids == null || ids.Length == 0 ? "none" : string.Join("-", ids.OrderBy(id => id));
    public string CacheKey => $"AvailableFilters-Search-{SearchTerm?.Trim().ToLower() ?? "all"}-Cats-{SerializeIds(CategoryIds)}-Brands-{SerializeIds(BrandIds)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products; // Filtreler ürün verisine dayalı
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10); // Filtreler 10 dk cache

    // --- Handler ---
    public class GetAvailableFiltersQueryHandler : IRequestHandler<GetAvailableFiltersQuery, List<FilterGroupDto>>
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<GetAvailableFiltersQueryHandler> _logger; // Logger eklendi

        public GetAvailableFiltersQueryHandler(
            IProductRepository productRepository,
             ILogger<GetAvailableFiltersQueryHandler> logger) // Logger eklendi
        {
            _productRepository = productRepository;
            _logger = logger; // Atandı
        }

        public async Task<List<FilterGroupDto>> Handle(GetAvailableFiltersQuery request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Fetching available filters for SearchTerm: '{SearchTerm}', CategoryIds: '{CategoryIds}', BrandIds: '{BrandIds}'",
                 request.SearchTerm ?? "N/A",
                 request.CategoryIds != null ? string.Join(",", request.CategoryIds) : "N/A",
                 request.BrandIds != null ? string.Join(",", request.BrandIds) : "N/A");

            // Repository metodunu çağır
            var filters = await _productRepository.GetAvailableFilters(
                request.SearchTerm,
                request.CategoryIds,
                request.BrandIds
                // cancellationToken repository metoduna geçirilmiyorsa burada eklenmeli.
            );

             _logger.LogInformation("Returning {Count} filter groups.", filters?.Count ?? 0);
            return filters ?? new List<FilterGroupDto>(); // Null dönerse boş liste
        }
    }
}