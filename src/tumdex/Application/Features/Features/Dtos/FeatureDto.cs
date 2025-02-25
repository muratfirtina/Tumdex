using Application.Features.FeatureValues.Dtos;
using Application.Features.Products.Dtos;

namespace Application.Features.Features.Dtos;

public class FeatureDto
{
    public string? Id { get; set; }
    public string? Name { get; set; } // Örneğin: "İletken"
    public ICollection<FeatureValueDto> FeatureValues { get; set; }
    
}