using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Persistence.Context;
using Persistence.Models;

namespace Persistence.Repositories;

public class OutboxRepository : EfRepositoryBase<OutboxMessage, string, TumdexDbContext>, IOutboxRepository
{
    private readonly OutboxSettings _settings;

    public OutboxRepository(
        TumdexDbContext context, 
        IOptions<OutboxSettings> settings) : base(context)
    {
        _settings = settings.Value;
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .Where(m => m.RetryCount < _settings.MaxRetryCount)
            .OrderBy(m => m.CreatedDate)
            .Take(_settings.BatchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var message = await Context.OutboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxStatus.Completed;
            message.ProcessedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await Context.OutboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.Status = OutboxStatus.Failed;
            message.Error = error;
            message.ProcessedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateRetryCountAsync(string messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await Context.OutboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.RetryCount++;
            message.Error = error;
            message.Status = message.RetryCount >= _settings.MaxRetryCount 
                ? OutboxStatus.Failed 
                : OutboxStatus.Pending;
            
            await Context.SaveChangesAsync(cancellationToken);
        }
    }
}