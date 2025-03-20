using Core.Persistence.Repositories;

namespace Domain.Entities;


public class Country : Entity<int>
{
    public string Code { get; set; }        // rewrite field from original ulkeler table
    public string Name { get; set; }         // baslik field from original ulkeler table
    public string PhoneCode { get; set; }    // alankodu field from original ulkeler table
    
    // Navigation properties
    public virtual ICollection<City> Cities { get; set; }
    public virtual ICollection<UserAddress> UserAddresses { get; set; }
    
    public Country()
    {
        Cities = new HashSet<City>();
        UserAddresses = new HashSet<UserAddress>();
    }
    
    public Country(int id, string code, string name, string phoneCode) : base()
    {
        Id = id;
        Code = code;
        Name = name;
        PhoneCode = phoneCode;
        Cities = new HashSet<City>();
    }
}