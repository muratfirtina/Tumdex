using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IOutboxRepository : IAsyncRepository<OutboxMessage, string>, IRepository<OutboxMessage, string>
{
    /// <summary>
    /// İşlenmemiş mesajları getirir
    /// </summary>
    Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesajı işlenmiş olarak işaretler
    /// </summary>
    Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesajı başarısız olarak işaretler
    /// </summary>
    Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry sayısını günceller
    /// </summary>
    Task UpdateRetryCountAsync(string messageId, string error, CancellationToken cancellationToken = default);
}