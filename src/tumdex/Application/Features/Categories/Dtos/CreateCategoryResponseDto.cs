using Application.Features.Features.Dtos;

namespace Application.Features.Categories.Dtos;

public class CreateCategoryResponseDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<FeatureDto> Features { get; set; }
}