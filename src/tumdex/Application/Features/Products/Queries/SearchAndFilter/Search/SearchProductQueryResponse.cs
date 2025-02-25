using Application.Extensions;
using Application.Features.Brands.Dtos;
using Application.Features.Categories.Dtos;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Core.Application.Responses;

namespace Application.Features.Products.Queries.SearchAndFilter.Search;

public class SearchProductQueryResponse: IResponse , IHasShowcaseImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string CategoryId { get; set; }
    public string CategoryName { get; set; }
    public string BrandId { get; set; }
    public string BrandName { get; set; }
    public string Sku { get; set; }
    public string VaryantGroupID { get; set; }
    public int Stock{ get; set; }
    public decimal Price { get; set; }
    public ICollection<ProductFeatureValueDto>? ProductFeatureValues { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
}

public class SearchResponse
{
    public GetListResponse<SearchProductQueryResponse> Products { get; set; }
    public List<CategoryDto> Categories { get; set; }
    public List<BrandDto> Brands { get; set; }
}