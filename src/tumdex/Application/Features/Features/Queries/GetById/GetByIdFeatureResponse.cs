using Application.Features.Categories.Dtos;
using Application.Features.FeatureValues.Dtos;
using Application.Features.Products.Dtos;
using Domain;

namespace Application.Features.Features.Queries.GetById;

public class GetByIdFeatureResponse
{
    public string Name { get; set; }
    public List<CategoryDto> Categories { get; set; }
    public List<FeatureValueDto> FeatureValues { get; set; }
}