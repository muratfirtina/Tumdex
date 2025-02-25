using Core.Application.Responses;

namespace Application.Features.PhoneNumbers.Commands.Update;

public class UpdatedPhoneNumberCommandResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
}