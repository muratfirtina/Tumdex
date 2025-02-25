using Core.Application.Responses;

namespace Application.Features.UserAddresses.Commands.DefaultAddress;

public class SetDefaultAddressCommandResponse : IResponse
{
    public bool Success { get; set; }
}