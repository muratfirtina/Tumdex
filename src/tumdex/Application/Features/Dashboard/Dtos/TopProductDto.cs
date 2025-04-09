using Application.Dtos.Image;
using Application.Features.ProductImageFiles.Dtos;

namespace Application.Features.Dashboard.Dtos;

public class TopProductDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
    public decimal Price { get; set; }
    public string BrandName { get; set; }
    public ImageFileDto Image { get; set; }
}