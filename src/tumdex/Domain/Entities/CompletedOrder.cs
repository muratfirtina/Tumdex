using Core.Persistence.Repositories;

namespace Domain.Entities;

public class CompletedOrder:Entity<string>
{
    public string OrderId { get; set; }

    public Order Order { get; set; }
}