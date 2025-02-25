using Application.Features.Categories.Dtos;

namespace Application.Features.Features.Dtos;

public class CreateFeatureDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<CreateCategoryResponseDto> Categories { get; set; }
}