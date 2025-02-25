using Core.Application.Responses;

namespace Application.Features.UserAddresses.Commands.Update;

public class UpdatedUserAddressCommandResponse:IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
}