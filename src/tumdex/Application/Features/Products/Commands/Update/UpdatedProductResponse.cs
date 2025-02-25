using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Core.Application.Responses;

namespace Application.Features.Products.Commands.Update;

public class UpdatedProductResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Title { get; set; }
    public string CategoryName { get; set; }
    public string BrandName { get; set; }
    public string? Description { get; set; }
    public ICollection<ProductFeatureDto>? ProductFeatures { get; set; }
    public List<ProductImageFileDto>? Images { get; set; } // Eklendi
}