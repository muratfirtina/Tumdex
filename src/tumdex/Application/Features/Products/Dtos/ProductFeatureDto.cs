using Application.Features.FeatureValues.Dtos;
using Domain;

namespace Application.Features.Products.Dtos;

public class ProductFeatureDto
{ 
    public string? Id { get; set; }
    public string? Name { get; set; } // Örneğin: "İletken"
    public ICollection<FeatureValueDto> FeatureValues { get; set; }
}