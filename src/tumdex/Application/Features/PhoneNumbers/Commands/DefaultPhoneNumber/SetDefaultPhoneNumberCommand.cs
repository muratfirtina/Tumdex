using Application.Repositories;
using MediatR;

namespace Application.Features.PhoneNumbers.Commands.DefaultPhoneNumber;

public class SetDefaultPhoneNumberCommand : IRequest<SetDefaultPhoneNumberCommandResponse>
{
    public string Id { get; set; }

    public class SetDefaultPhoneNumberCommandHandler : IRequestHandler<SetDefaultPhoneNumberCommand, SetDefaultPhoneNumberCommandResponse>
    {
        private readonly IPhoneNumberRepository _phoneNumberRepository;

        public SetDefaultPhoneNumberCommandHandler(IPhoneNumberRepository phoneNumberRepository)
        {
            _phoneNumberRepository = phoneNumberRepository;
        }

        public async Task<SetDefaultPhoneNumberCommandResponse> Handle(SetDefaultPhoneNumberCommand request, CancellationToken cancellationToken)
        {
            var result = await _phoneNumberRepository.SetDefaultPhoneAsync(request.Id);
            return new SetDefaultPhoneNumberCommandResponse { Success = result };
        }
    }
}