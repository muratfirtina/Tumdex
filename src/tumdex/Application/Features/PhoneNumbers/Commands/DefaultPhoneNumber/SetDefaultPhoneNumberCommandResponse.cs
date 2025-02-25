using Core.Application.Responses;

namespace Application.Features.PhoneNumbers.Commands.DefaultPhoneNumber;

public class SetDefaultPhoneNumberCommandResponse : IResponse
{
    public bool Success { get; set; }
}