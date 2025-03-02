using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class NewsletterLogRepository : EfRepositoryBase<NewsletterLog, string, TumdexDbContext>, INewsletterLogRepository
{
    public NewsletterLogRepository(TumdexDbContext context) : base(context) { }

    public async Task<NewsletterLog> LogNewsletterSendAsync(NewsletterLog log)
    {
        await AddAsync(log);
        return log;
    }

    public async Task<List<NewsletterLog>> GetLogsForPeriodAsync(DateTime startDate, DateTime endDate)
    {
        return await Context.NewsletterLogs
            .Where(l => l.SentDate >= startDate && l.SentDate <= endDate)
            .OrderByDescending(l => l.SentDate)
            .ToListAsync();
    }
}