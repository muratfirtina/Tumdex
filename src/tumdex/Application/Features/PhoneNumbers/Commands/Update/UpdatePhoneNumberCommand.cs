using Application.Features.PhoneNumbers.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.PhoneNumbers.Commands.Update;

public class UpdatePhoneNumberCommand : IRequest<UpdatedPhoneNumberCommandResponse>,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Number { get; set; }
    public bool IsDefault { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => "PhoneNumbers";

    public class UpdatePhoneNumberCommandHandler : IRequestHandler<UpdatePhoneNumberCommand, UpdatedPhoneNumberCommandResponse>
    {
        private readonly IPhoneNumberRepository _phoneNumberRepository;
        private readonly IMapper _mapper;

        public UpdatePhoneNumberCommandHandler(IPhoneNumberRepository phoneNumberRepository, IMapper mapper)
        {
            _phoneNumberRepository = phoneNumberRepository;
            _mapper = mapper;
        }

        public async Task<UpdatedPhoneNumberCommandResponse> Handle(UpdatePhoneNumberCommand request, CancellationToken cancellationToken)
        {
            var phoneDto = _mapper.Map<UpdatePhoneNumberDto>(request);
            var phone = await _phoneNumberRepository.UpdatePhoneAsync(phoneDto);
            return _mapper.Map<UpdatedPhoneNumberCommandResponse>(phone);
        }
    }
}