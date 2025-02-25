using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain;

public class UserAddress : Entity<string>
{
    public string? Name { get; set; } // Adres başlığı (Ev, iş vb.)
    public string? UserId { get; set; }
    public AppUser User { get; set; } // Adresin sahibini belirler
        
    public string? AddressLine1 { get; set; } // Adresin ana kısmı (Sokak, apartman vb.)
    public string? AddressLine2 { get; set; } // Adresin ek kısmı (Daire, kat vb. isteğe bağlı)
    public string? City { get; set; } // Şehir
    public string? State { get; set; } // İlçe veya bölge
    public string? PostalCode { get; set; } // Posta kodu
    public string? Country { get; set; } // Ülke
    public bool IsDefault { get; set; } // Varsayılan adres olup olmadığını belirtir

    public UserAddress(string? name):base(name)
    {
        Name = name;
    }
   
}