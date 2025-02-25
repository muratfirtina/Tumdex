using Core.Application.Responses;

namespace Application.Features.PhoneNumbers.Commands.Create;

public class CreatedPhoneNumberCommandResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
}