using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface INewsletterRepository : IAsyncRepository<Newsletter, string>, IRepository<Newsletter, string>
{
    Task<bool> IsEmailSubscribedAsync(string email);
    Task<List<Newsletter>> GetActiveSubscribersAsync();
    Task<Newsletter> SubscribeAsync(string email, string? source = null, string? userId = null);
    Task<Newsletter> UnsubscribeAsync(string email);
    Task<NewsletterLog> LogNewsletterSendAsync(NewsletterLog log);
}