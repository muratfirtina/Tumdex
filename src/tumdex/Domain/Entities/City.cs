using Core.Persistence.Repositories;

namespace Domain.Entities;

public class City : Entity<int>
{
    public int CountryId { get; set; }
    public string Name { get; set; }
    public string? Code { get; set; }
    
    // Navigation properties
    public virtual Country Country { get; set; }
    public virtual ICollection<District> Districts { get; set; }
    public virtual ICollection<UserAddress> UserAddresses { get; set; }
    
    public City()
    {
        Districts = new HashSet<District>();
        UserAddresses = new HashSet<UserAddress>();
    }
    
    public City(int id, int countryId, string name, string? code) : base()
    {
        Id = id;
        CountryId = countryId;
        Name = name;
        Code = code;
        Districts = new HashSet<District>();
    }
}