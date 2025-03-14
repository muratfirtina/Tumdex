using Core.Persistence.Repositories;

namespace Domain.Entities;

public class ACMenu : Entity<string> //autohorization - controller menu
{
    public string Name { get; set; }
    public ICollection<Endpoint> Endpoints { get; set; }
}