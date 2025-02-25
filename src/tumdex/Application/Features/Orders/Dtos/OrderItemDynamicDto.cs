using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;

namespace Application.Features.Orders.Dtos;

public class OrderItemDynamicDto
{
    public string Id { get; set; }
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string? ProductTitle { get; set; }
    public string? BrandName { get; set; }
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? UpdatedPrice { get; set; }
    public int? LeadTime { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
    public List<ProductFeatureValueDto> ProductFeatureValues { get; set; }
}