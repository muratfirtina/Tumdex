using Application.Abstraction.Services;
using Application.Services;
using Domain;
using Domain.Entities;
using Persistence.Context;

namespace Persistence.Services;

public class ContactService : IContactService
{
    private readonly TumdexDbContext _context;
    private readonly IOrderEmailService _orderEmailService; // İsteğe bağlı: Email gönderimi için

    public ContactService(TumdexDbContext context, IOrderEmailService orderEmailService)
    {
        _context = context;
        _orderEmailService = orderEmailService;
    }

    public async Task CreateAsync(Contact contact)
    {
        contact.Id = Guid.NewGuid().ToString();
        contact.CreatedDate = DateTime.UtcNow;
        contact.IsRead = false;

        await _context.Contacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // İsteğe bağlı: Yöneticiye bildirim emaili gönder
        await _orderEmailService.SendEmailAsync(
            "muratfirtina@hotmail.com",
            "New Contact Form Submission",
            $"Name: {contact.Name}\nEmail: {contact.Email}\nSubject: {contact.Subject}\nMessage: {contact.Message}"
        );
    }
}