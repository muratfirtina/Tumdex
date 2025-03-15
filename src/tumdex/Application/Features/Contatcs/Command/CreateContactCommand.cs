using Application.Abstraction.Services.Email;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Transaction;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contatcs.Command;

public class CreateContactCommand : IRequest<CreatedContactResponse>, ITransactionalRequest
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    
    public class CreateContactCommandHandler : IRequestHandler<CreateContactCommand, CreatedContactResponse>
    {
        private readonly IContactRepository _contactRepository;
        private readonly IMapper _mapper;
        private readonly IContactEmailService _contactEmailService;
        private readonly ILogger<CreateContactCommandHandler> _logger;

        public CreateContactCommandHandler(
            IContactRepository contactRepository, 
            IMapper mapper, 
            IContactEmailService contactEmailService,
            ILogger<CreateContactCommandHandler> logger)
        {
            _contactRepository = contactRepository;
            _mapper = mapper;
            _contactEmailService = contactEmailService;
            _logger = logger;
        }

        public async Task<CreatedContactResponse> Handle(CreateContactCommand request, CancellationToken cancellationToken)
        {
            // Command'den Contact entity'sine mapping
            Contact contact = _mapper.Map<Contact>(request);
            
            // Contact'ı veritabanına kaydet
            await _contactRepository.AddAsync(contact);
            
            // İletişim formu bilgilerini e-posta olarak gönder
            try
            {
                await _contactEmailService.SendContactFormEmailAsync(
                    request.Name,
                    request.Email,
                    request.Subject,
                    request.Message
                );
                
                _logger.LogInformation("Contact form notification email sent successfully for {Email}", request.Email);
            }
            catch (Exception ex)
            {
                // E-posta gönderimi başarısız olduğunda işlemi tamamen başarısız sayıyoruz
                // ITransactionalRequest sayesinde veritabanı işlemi de geri alınacaktır
                _logger.LogError(ex, "Failed to send contact form notification email for {Email}", request.Email);
                
                // Exception fırlatarak işlemin tamamen başarısız olmasını sağlıyoruz
                throw new ApplicationException("Contact form saved but email notification failed. Operation rolled back.", ex);
            }
            
            // Contact'dan Response'a mapping yaparak sonucu döndür
            CreatedContactResponse response = _mapper.Map<CreatedContactResponse>(contact);
            return response;
        }
    }
}