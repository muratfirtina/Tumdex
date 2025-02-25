using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Transaction;
using Domain;
using MediatR;

namespace Application.Features.Contatcs.Command;

public class CreateContactCommand : IRequest<CreatedContactResponse>,ITransactionalRequest
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    
    public class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, CreatedContactResponse>
    {
        private readonly IContactRepository _contactRepository;
        private readonly IMapper _mapper;

        public CreateContactCommandHandler(IContactRepository contactRepository, IMapper mapper)
        {
            _contactRepository = contactRepository;
            _mapper = mapper;
        }

        public async Task<CreatedContactResponse> Handle(CreateContactCommand request, CancellationToken cancellationToken)
        {
            Contact contact = _mapper.Map<Contact>(request); // Command'den Contact'a mapping
            await _contactRepository.AddAsync(contact);
            CreatedContactResponse response = _mapper.Map<CreatedContactResponse>(contact); // Contact'dan Response'a mapping
            return response;
        }
    }
}
