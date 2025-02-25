using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain;

public class Endpoint:Entity<string>
{
    public Endpoint()
    {
        Roles = new HashSet<AppRole>();
    }
    public string ActionType { get; set; }
    public string HttpType { get; set; }
    public string Definition { get; set; }
    public string Code { get; set; }
    public ACMenu AcMenu { get; set; }
    public ICollection<AppRole> Roles { get; set; }
}