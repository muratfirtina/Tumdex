using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class NewsletterRepository : EfRepositoryBase<Newsletter, string, TumdexDbContext>, INewsletterRepository
{
    public NewsletterRepository(TumdexDbContext context) : base(context) { }

    public async Task<bool> IsEmailSubscribedAsync(string email)
    {
        return await Context.Newsletters
            .AnyAsync(n => n.Email.ToLower() == email.ToLower() && n.IsSubscribed);
    }

    public async Task<List<Newsletter>> GetActiveSubscribersAsync()
    {
        return await Context.Newsletters
            .Where(n => n.IsSubscribed)
            .ToListAsync();
    }

    public async Task<Newsletter> SubscribeAsync(string email, string? source = null, string? userId = null)
    {
        var existingSubscriber = await Context.Newsletters
            .FirstOrDefaultAsync(n => n.Email.ToLower() == email.ToLower());

        if (existingSubscriber != null)
        {
            if (!existingSubscriber.IsSubscribed)
            {
                existingSubscriber.IsSubscribed = true;
                existingSubscriber.UnsubscriptionDate = null;
                existingSubscriber.Source = source;
                existingSubscriber.UserId = userId;
                await UpdateAsync(existingSubscriber);
            }
            return existingSubscriber;
        }

        var newsletter = new Newsletter
        {
            Email = email,
            Source = source,
            UserId = userId
        };

        return await AddAsync(newsletter);
    }

    public async Task<Newsletter> UnsubscribeAsync(string email)
    {
        var subscriber = await Context.Newsletters
            .FirstOrDefaultAsync(n => n.Email.ToLower() == email.ToLower());

        if (subscriber == null)
            throw new Exception("Subscriber not found");

        subscriber.IsSubscribed = false;
        subscriber.UnsubscriptionDate = DateTime.UtcNow;

        await UpdateAsync(subscriber);
        return subscriber;
    }

    public async Task<NewsletterLog> LogNewsletterSendAsync(NewsletterLog log)
    {
        await Context.NewsletterLogs.AddAsync(log);
        await Context.SaveChangesAsync();
        return log;
    }
}