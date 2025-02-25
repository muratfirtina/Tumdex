using Microsoft.AspNetCore.Identity;

namespace Domain.Identity;

public class AppUser : IdentityUser<string>
{
    public string NameSurname { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenEndDateTime { get; set; }
    public ICollection<Cart> Carts { get; set; }
    public ICollection<UserAddress>? UserAddresses { get; set; }
    public ICollection<ProductLike>? ProductLikes { get; set; }
    public ICollection<ProductView>? ProductViews { get; set; }
    public ICollection<PhoneNumber>? PhoneNumbers { get; set; }
}