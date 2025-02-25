using Core.Persistence.Repositories;
using Domain.Enum;

namespace Domain;

public class OutboxMessage : Entity<string>
{
    public string Type { get; set; }
    public string Data { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public OutboxStatus Status { get; set; }

    // Entity base class'ından gelen DeletedDate'i kullanmıyoruz
    // Çünkü hard delete yapacağız

    public OutboxMessage()
    {
        Id = Guid.NewGuid().ToString();
        Status = OutboxStatus.Pending;
        RetryCount = 0;
    }

    public OutboxMessage(string type, string data) : this()
    {
        Type = type;
        Data = data;
    }
}