using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain.Entities;

public class UserAddress : Entity<string>
{
    public string? Name { get; set; } // Adres başlığı (Ev, iş vb.)
    public string? UserId { get; set; }
    
    public string? AddressLine1 { get; set; } // Adresin ana kısmı (Sokak, apartman vb.)
    public string? AddressLine2 { get; set; } // Adresin ek kısmı (Daire, kat vb. isteğe bağlı)
    
    // Foreign Key alanları
    public int? CountryId { get; set; }
    public int? CityId { get; set; }
    public int? DistrictId { get; set; }
    
    public string? PostalCode { get; set; } // Posta kodu
    public bool IsDefault { get; set; } // Varsayılan adres olup olmadığını belirtir

    // Navigation properties
    public AppUser User { get; set; } // Adresin sahibini belirler
    public Country? Country { get; set; }
    public City? City { get; set; }
    public District? District { get; set; }

    public UserAddress(string? name):base(name)
    {
        Name = name;
    }
}