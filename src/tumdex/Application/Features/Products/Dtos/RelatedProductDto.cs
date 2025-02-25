using Application.Extensions;
using Application.Features.ProductImageFiles.Dtos;

namespace Application.Features.Products.Dtos;

public class RelatedProductDto : IHasShowcaseImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public float? Price { get; set; }
    public int? Stock { get; set; }
    public string? Sku { get; set; }
    public string? CategoryName { get; set; }
    public string? BrandName { get; set; }
    public List<ProductFeatureValueDto> ProductFeatureValues { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
}