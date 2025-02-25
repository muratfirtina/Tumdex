using Application.Features.Categories.Dtos;
using Application.Features.Features.Dtos;
using Core.Application.Responses;

namespace Application.Features.Categories.Commands.Create;

public class CreatedCategoryResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<FeatureDto>? Features { get; set; }
    public CategoryImageFileDto? CategoryImage { get; set; }
}