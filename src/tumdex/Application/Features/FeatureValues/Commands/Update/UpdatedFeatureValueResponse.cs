using Core.Application.Responses;

namespace Application.Features.FeatureValues.Commands.Update;

public class UpdatedFeatureValueResponse : IResponse
{
    public string Id { get; set; }  
    public string Name { get; set; }
    public string FeatureId { get; set; }
}