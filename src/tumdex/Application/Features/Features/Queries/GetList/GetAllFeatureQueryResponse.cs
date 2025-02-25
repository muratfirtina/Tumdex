using Application.Features.FeatureValues.Dtos;
using Application.Features.Products.Dtos;

namespace Application.Features.Features.Queries.GetList;

public class GetAllFeatureQueryResponse
{
    public string Id { get; set; } 
    public string Name { get; set; }
    public ICollection<FeatureValueDto> FeatureValues { get; set; }
}