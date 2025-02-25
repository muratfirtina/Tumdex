using Core.Application.Responses;

namespace Application.Features.PhoneNumbers.Commands.Delete;

public class DeletedPhoneNumberCommandResponse : IResponse
{
    public bool Success { get; set; }
}