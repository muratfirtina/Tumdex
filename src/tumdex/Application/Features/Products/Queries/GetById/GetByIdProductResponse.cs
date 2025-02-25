using Application.Extensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Core.Application.Responses;

namespace Application.Features.Products.Queries.GetById;

public class GetByIdProductResponse : IResponse , IHasRelatedProducts , IHasProductImageFiles
{
    public string Id { get; set; }
    public string Name { get; set; }
    public float? Price { get; set; }
    public string? Description { get; set; }
    public string? Title { get; set; }
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? BrandId { get; set; }
    public string? BrandName { get; set; }
    public int? Stock { get; set; } = 0;
    public int? Tax { get; set; }
    public string VaryantGroupID { get; set; }
    public string? Sku { get; set; }
    public ICollection<ProductImageFileDto>? ProductImageFiles { get; set; }
    public ICollection<ProductFeatureValueDto>? ProductFeatureValues { get; set; }
    public List<RelatedProductDto> RelatedProducts { get; set; }
    public Dictionary<string, List<string>> AvailableFeatures { get; set; }
}