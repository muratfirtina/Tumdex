using Core.Persistence.Repositories;

namespace Domain;

public class CompletedOrder:Entity<string>
{
    public string OrderId { get; set; }

    public Order Order { get; set; }
}