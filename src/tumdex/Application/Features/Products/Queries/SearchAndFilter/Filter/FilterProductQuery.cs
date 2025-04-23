using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.Queries.SearchAndFilter.Filter;

public class FilterProductQuery : IRequest<GetListResponse<FilterProductQueryResponse>>, ICachableRequest
{
    public string? SearchTerm { get; set; }
    public PageRequest? PageRequest { get; set; } // Nullable yapıldı - sayfalama isteğe bağlı
    public Dictionary<string, List<string>>? Filters { get; set; }
    public string SortOrder { get; set; } = "default";

    // ICachableRequest implementation
    private string SerializeFilters()
    {
        if (Filters == null || !Filters.Any()) return "nofilters";
        // Deterministik hale getirmek için sıralama
        var orderedFilters = new SortedDictionary<string, List<string>>(Filters);
        foreach (var key in orderedFilters.Keys) orderedFilters[key].Sort();
        try
        {
            return JsonSerializer.Serialize(orderedFilters);
        }
        catch
        {
            return "filter-serialization-error";
        }
    }

    public string CacheKey => PageRequest == null
        ? $"Products-Filter-{SearchTerm?.Trim().ToLower() ?? "all"}-NoPagination-Sort{SortOrder}-{SerializeFilters()}"
        : $"Products-Filter-{SearchTerm?.Trim().ToLower() ?? "all"}-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}-Sort{SortOrder}-{SerializeFilters()}";

    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Products;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(10);

    // --- Handler ---
    public class FilterProductQueryHandler : IRequestHandler<FilterProductQuery, GetListResponse<FilterProductQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly ILogger<FilterProductQueryHandler> _logger;

        public FilterProductQueryHandler(
            IProductRepository productRepository,
            IStorageService storageService,
            IMapper mapper,
            ILogger<FilterProductQueryHandler> logger)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<GetListResponse<FilterProductQueryResponse>> Handle(FilterProductQuery request,
            CancellationToken cancellationToken)
        {
            if (request.PageRequest == null)
            {
                _logger.LogInformation(
                    "Executing FilterProductQuery without pagination. Search: '{SearchTerm}', Sort: {Sort}, Filters: {Filters}",
                    request.SearchTerm ?? "N/A", request.SortOrder, request.SerializeFilters());
            }
            else
            {
                _logger.LogInformation(
                    "Executing FilterProductQuery with pagination. Search: '{SearchTerm}', Page: {Page}, Size: {Size}, Sort: {Sort}, Filters: {Filters}",
                    request.SearchTerm ?? "N/A", request.PageRequest.PageIndex, request.PageRequest.PageSize,
                    request.SortOrder, request.SerializeFilters());
            }

            // Sayfalama olmadan mevcut metodu kullan
            IPaginate<Product> products = await _productRepository.FilterProductsAsync(
                request.SearchTerm,
                request.Filters,
                request.PageRequest, // Direkt null geçebiliriz
                request.SortOrder
            );

            if (products == null || products.Items == null)
            {
                _logger.LogWarning("Product filtering returned null or empty items.");
                return new GetListResponse<FilterProductQueryResponse>
                    { Items = new List<FilterProductQueryResponse>() };
            }

            GetListResponse<FilterProductQueryResponse> response =
                _mapper.Map<GetListResponse<FilterProductQueryResponse>>(products);

            if (response.Items != null && response.Items.Any())
            {
                foreach (var productDto in response.Items)
                {
                    var productEntity = products.Items.FirstOrDefault(p => p.Id == productDto.Id);
                    if (productEntity?.ProductImageFiles != null)
                    {
                        var showcaseImage = productEntity.ProductImageFiles.FirstOrDefault(pif => pif.Showcase);
                        if (showcaseImage != null)
                        {
                            productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                        }
                    }
                }
            }

            _logger.LogInformation("Returning {Count} filtered products. Total items: {TotalCount}",
                response.Items?.Count ?? 0, response.Count);
            return response;
        }
    }
}