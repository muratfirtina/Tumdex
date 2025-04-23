using Application.Consts;
using Application.Repositories;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.PhoneNumbers.Commands.Delete;

public class DeletePhoneNumberCommand : IRequest<DeletedPhoneNumberCommandResponse>,ICacheRemoverRequest
{
    public string Id { get; set; }

    public string CacheKey => $"PhoneNumber-{Id}"; // Nadiren ID ile sorgulanır ama ekleyelim.
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.UserPhoneNumbers;

    public class DeletePhoneNumberCommandHandler : IRequestHandler<DeletePhoneNumberCommand, DeletedPhoneNumberCommandResponse>
    {
        private readonly IPhoneNumberRepository _phoneNumberRepository;

        public DeletePhoneNumberCommandHandler(IPhoneNumberRepository phoneNumberRepository)
        {
            _phoneNumberRepository = phoneNumberRepository;
        }

        public async Task<DeletedPhoneNumberCommandResponse> Handle(DeletePhoneNumberCommand request, CancellationToken cancellationToken)
        {
            var result = await _phoneNumberRepository.DeletePhoneAsync(request.Id);
            return new DeletedPhoneNumberCommandResponse { Success = result };
        }
    }
}