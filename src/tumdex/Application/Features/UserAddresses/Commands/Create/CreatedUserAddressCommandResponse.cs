using Core.Application.Responses;

namespace Application.Features.UserAddresses.Commands.Create;

public class CreatedUserAddressCommandResponse:IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
}