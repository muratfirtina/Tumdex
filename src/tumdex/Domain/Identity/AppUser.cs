using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace Domain.Identity;

public class AppUser : IdentityUser<string>
{
    public string NameSurname { get; set; }
    
    // Token bilgileri - RefreshTokenEndDateTime yerine RefreshTokenExpiryTime kullanıldı
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; } // TokenHandler ile uyumlu olması için isim değiştirildi
    
    public bool IsActive { get; set; } = true; 
    
    // İlişkiler
    public ICollection<Cart> Carts { get; set; }
    public ICollection<UserAddress>? UserAddresses { get; set; }
    public ICollection<ProductLike>? ProductLikes { get; set; }
    public ICollection<ProductView>? ProductViews { get; set; }
    public ICollection<PhoneNumber>? PhoneNumbers { get; set; }
    
    // Zamanlama alanları
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    
    // RefreshToken ilişkisi için eklendi
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    
    // Güvenlik için ek alanlar
    public string? LastLoginIp { get; set; }
    public string? LastLoginUserAgent { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public string? SessionId { get; set; } = Guid.NewGuid().ToString();
    
    // Yeni bir RefreshToken'ı ana RefreshToken olarak ayarlamak için yardımcı metot
    public void SetRefreshToken(RefreshToken refreshToken)
    {
        RefreshToken = refreshToken.Token;
        RefreshTokenExpiryTime = refreshToken.ExpiryDate;
        
        // Güvenlik bilgilerini güncelle
        LastLoginIp = refreshToken.CreatedByIp;
        LastLoginUserAgent = refreshToken.UserAgent;
        LastLoginDate = DateTime.UtcNow;
        
        // Başarılı giriş sonrası başarısız giriş sayısını sıfırla
        FailedLoginAttempts = 0;
    }
}