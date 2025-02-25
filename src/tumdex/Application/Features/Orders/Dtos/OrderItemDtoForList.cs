using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;

namespace Application.Features.Orders.Dtos;

public class OrderItemDtoForList
{
    public string Id { get; set; }
    public string ProductName { get; set; }
    public string ProductTitle { get; set; }
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? UpdatedPrice { get; set; }
    public string BrandName { get; set; }
    public ICollection<ProductFeatureValueDto> ProductFeatureValues { get; set; }
    public ProductImageFileDto ShowcaseImage { get; set; }
}