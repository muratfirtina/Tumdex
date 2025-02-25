using Core.Application.Responses;

namespace Application.Features.Features.Commands.Update;

public class UpdatedFeatureResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
}