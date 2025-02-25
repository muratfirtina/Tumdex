using Application.Features.ProductImageFiles.Dtos;

namespace Application.Features.Orders.Dtos;

public class OrderCartItemDto
{
    public string Name { get; set; }
    public float Price { get; set; }
    public int Quantity { get; set; }
    public float TotalPrice { get; set; }
    public List<ProductImageFileDto>? ProductImageFiles { get; set; }
}