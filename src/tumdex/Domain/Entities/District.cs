using Core.Persistence.Repositories;

namespace Domain.Entities;

public class District : Entity<int>
{
    public int CityId { get; set; }
    public string Name { get; set; }
    public string? Code { get; set; }
    
    // Navigation property
    public virtual City City { get; set; }
    public virtual ICollection<UserAddress> UserAddresses { get; set; }
    
    public District()
    {
        UserAddresses = new HashSet<UserAddress>();
    }
    
    public District(int id, int cityId, string name, string? code) : base()
    {
        Id = id;
        CityId = cityId;
        Name = name;
        Code = code;
    }
}