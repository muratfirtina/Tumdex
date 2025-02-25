using Application.Features.ProductImageFiles.Dtos;

namespace Application.Features.Orders.Dtos;


public class OrderItemUpdateDto
{
    public string Id { get; set; }
    public string? ProductName { get; set; }
    public string? BrandName { get; set; }
    public decimal? Price { get; set; }
    public decimal? UpdatedPrice { get; set; }
    public int? Quantity { get; set; }
    public int? LeadTime { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
}