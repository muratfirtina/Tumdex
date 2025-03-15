using System.Text.Json;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Enums;
using Application.Models.Queue.Email;
using Domain;
using Domain.Entities;
using Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Infrastructure.Services.Mail;

public class EmailQueueService : IEmailQueueService
{
    private readonly IEmailService _defaultMailService; // Varsayılan e-posta servisi
    private readonly IAccountEmailService _accountEmailService; // Hesap işlemleri için
    private readonly IOrderEmailService _orderEmailService; // Sipariş işlemleri için
    private readonly IContactEmailService _contactEmailService; // İletişim formu için
    private readonly ILogger<EmailQueueService> _logger;
    private readonly TumdexDbContext _dbContext;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string QUEUE_TYPE = "EmailMessage";
    private const int MAX_RETRY_COUNT = 3;

    public EmailQueueService(
        IEmailService defaultMailService,
        IAccountEmailService accountEmailService,
        IOrderEmailService orderEmailService,
        ILogger<EmailQueueService> logger,
        TumdexDbContext dbContext, IContactEmailService contactEmailService)
    {
        _defaultMailService = defaultMailService;
        _accountEmailService = accountEmailService;
        _orderEmailService = orderEmailService;
        _logger = logger;
        _dbContext = dbContext;
        _contactEmailService = contactEmailService;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task QueueEmailAsync(string to, string subject, string body, EmailType type, Dictionary<string, string> metadata = null)
    {
        try
        {
            var emailMessage = new EmailQueueMessage
            {
                To = to,
                Subject = subject,
                Body = body,
                Type = type,
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            // OutboxMessages tablosuna ekle
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = QUEUE_TYPE,
                Data = JsonSerializer.Serialize(emailMessage, _jsonOptions),
                CreatedDate = DateTime.UtcNow,
                Status = OutboxStatus.Pending,
                RetryCount = 0
            };

            await _dbContext.OutboxMessages.AddAsync(outboxMessage);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Email to {Email} queued successfully. Type: {Type}", to, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue email to {Email}", to);
            throw;
        }
    }

    public async Task QueueEmailConfirmationAsync(string email, string userId, string token)
    {
        // E-posta doğrulama için gerekli metadata
        var metadata = new Dictionary<string, string>
        {
            { "userId", userId },
            { "token", token }
        };

        // Bu noktada subject ve body boş bırakıyoruz, bunları işlem sırasında oluşturacağız
        await QueueEmailAsync(email, "Email Confirmation", "", EmailType.EmailConfirmation, metadata);
    }

    public async Task QueuePasswordResetAsync(string email, string userId, string token)
    {
        var metadata = new Dictionary<string, string>
        {
            { "userId", userId },
            { "token", token }
        };

        await QueueEmailAsync(email, "Password Reset", "", EmailType.PasswordReset, metadata);
    }

    public Task QueueContactFormEmailAsync(string name, string email, string subject, string message)
    {
        // İletişim formu için gerekli metadata
        var metadata = new Dictionary<string, string>
        {
            { "name", name },
            { "email", email },
            { "subject", subject },
            { "message", message }
        };

        // Bu noktada subject ve body boş bırakıyoruz, bunları işlem sırasında oluşturacağız
        return QueueEmailAsync(email, "Contact Form", "", EmailType.ContactForm, metadata);
    }

    public async Task ProcessQueuedEmailsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // OutboxMessages tablosundan bekleyen e-posta mesajlarını al
            var pendingEmails = await _dbContext.OutboxMessages
                .Where(m => m.Type == QUEUE_TYPE && 
                           m.Status == OutboxStatus.Pending && 
                           m.RetryCount < MAX_RETRY_COUNT && 
                           m.DeletedDate == null)
                .OrderBy(m => m.CreatedDate)
                .Take(20) // İşlem sınırı
                .ToListAsync(cancellationToken);

            foreach (var emailMessage in pendingEmails)
            {
                try
                {
                    var message = JsonSerializer.Deserialize<EmailQueueMessage>(emailMessage.Data, _jsonOptions);
                    
                    // E-posta tipine göre işlem yap
                    switch (message.Type)
                    {
                        case EmailType.EmailConfirmation:
                        case EmailType.PasswordReset:
                            await ProcessAccountEmailAsync(message);
                            break;
                        case EmailType.OrderConfirmation:
                        case EmailType.OrderUpdate:
                            await ProcessOrderEmailAsync(message);
                            break;
                        case EmailType.ContactForm: // YENİ: İletişim formu e-postaları için yeni işleme
                            await ProcessContactFormEmailAsync(message);
                            break;
                        default:
                            // Diğer tipler için varsayılan e-posta servisi
                            await _defaultMailService.SendEmailAsync(message.To, message.Subject, message.Body);
                            break;
                    }

                    // Başarılı olarak işaretle
                    emailMessage.Status = OutboxStatus.Completed;
                    emailMessage.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing email message {Id}", emailMessage.Id);
                    
                    // Başarısız denemeyi kaydet
                    emailMessage.RetryCount++;
                    emailMessage.Error = ex.Message;
                    
                    // MAX_RETRY_COUNT'a ulaşıldıysa, başarısız olarak işaretle
                    if (emailMessage.RetryCount >= MAX_RETRY_COUNT)
                    {
                        emailMessage.Status = OutboxStatus.Failed;
                    }
                }

                // Durumu güncelle
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email queue");
            throw;
        }
    }

    private async Task ProcessAccountEmailAsync(EmailQueueMessage message)
    {
        if (!message.Metadata.TryGetValue("userId", out var userId) ||
            !message.Metadata.TryGetValue("token", out var token))
        {
            throw new InvalidOperationException("Missing userId or token in account email metadata");
        }

        switch (message.Type)
        {
            case EmailType.EmailConfirmation:
                await _accountEmailService.SendEmailConfirmationAsync(message.To, userId, token);
                break;
            case EmailType.PasswordReset:
                await _accountEmailService.SendPasswordResetEmailAsync(message.To, userId, token);
                break;
            default:
                throw new InvalidOperationException($"Unsupported account email type: {message.Type}");
        }
    }

    private async Task ProcessOrderEmailAsync(EmailQueueMessage message)
    {
        // Sipariş ile ilgili e-postaları doğrudan OrderEmailService'a gönder
        // Burada ihtiyaca göre OrderEmailService'in uygun metodunu çağırmak gerekir
        
        if (message.Type == EmailType.OrderConfirmation || message.Type == EmailType.OrderUpdate)
        {
            // Şimdilik sadece genel e-posta olarak gönder, ileride gerçek sipariş bilgilerini
            // metadata'dan alıp doğru metodu çağıracak şekilde güncellenebilir
            await _orderEmailService.SendEmailAsync(message.To, message.Subject, message.Body);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported order email type: {message.Type}");
        }
    }
    private async Task ProcessContactFormEmailAsync(EmailQueueMessage message)
    {
        if (!message.Metadata.TryGetValue("name", out var name) ||
            !message.Metadata.TryGetValue("email", out var email) ||
            !message.Metadata.TryGetValue("message", out var messageBody))
        {
            throw new InvalidOperationException("Missing contact form metadata");
        }

        await _contactEmailService.SendContactFormEmailAsync(name, email, message.Subject, messageBody);
    }
}