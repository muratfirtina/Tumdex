using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface INewsletterLogRepository : IAsyncRepository<NewsletterLog, string>, IRepository<NewsletterLog, string>
{
    Task<NewsletterLog> LogNewsletterSendAsync(NewsletterLog log);
    Task<List<NewsletterLog>> GetLogsForPeriodAsync(DateTime startDate, DateTime endDate);
}