using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain;

public class PhoneNumber : Entity<string>
{
    public string Name { get; set; }
    public string UserId { get; set; }
    public AppUser User { get; set; }
    public string Number { get; set; }
    public bool IsDefault { get; set; }
    
    public PhoneNumber(string name):base(name)
    {
        Name = name;
    }
}