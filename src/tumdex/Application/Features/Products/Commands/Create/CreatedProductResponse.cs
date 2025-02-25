using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Core.Application.Responses;

namespace Application.Features.Products.Commands.Create;

public class CreatedProductResponse : IResponse
{
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string CategoryId { get; set; }
    public string BrandId { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int Stock { get; set; }
    public int? Tax { get; set; }
    public string? VaryantGroupID { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<string>? FeatureValueIds { get; set; }
    public List<ProductImageFileDto>? Images { get; set; }

}