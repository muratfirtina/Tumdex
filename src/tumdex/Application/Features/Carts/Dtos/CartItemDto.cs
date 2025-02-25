using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;

namespace Application.Features.Carts.Dtos;

public class CartItemDto
{
    public string? CartItemId { get; set; }
    public string? CartId { get; set; }
    public string? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductTitle { get; set; }
    public string? BrandName { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool IsChecked { get; set; }
    public List<ProductFeatureValueDto>? ProductFeatureValues { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
}