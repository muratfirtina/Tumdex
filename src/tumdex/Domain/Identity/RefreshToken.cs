using System;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain.Identity;

public class RefreshToken : Entity<string>
{
    // Token hash'i veritabanında saklanır
    public string TokenHash { get; set; }

    // Orijinal token client'a gönderilir ve sadece oluşturma sırasında kullanılır
    // Bu alan veritabanında saklanmaz - NotMapped attributü eklendi
    [NotMapped] public string Token { get; private set; }

    public DateTime ExpiryDate { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? CreatedDate { get; set; }

    // Kullanıcı bilgileri
    public string UserId { get; set; }
    public virtual AppUser User { get; set; }

    // Güvenlik için ek bilgiler
    public string CreatedByIp { get; set; }
    public string UserAgent { get; set; }
    public string JwtId { get; set; } // Token'ın Jti claim'i

    // Ailelendirme için (isteğe bağlı)
    public string? FamilyId { get; set; }

    // İptal bilgileri
    public string? RevokedByIp { get; set; }
    public string? ReasonRevoked { get; set; }
    public DateTime? RevokedDate { get; set; }

    // Token aktif mi kontrolü
    public bool IsActive => !IsRevoked && !IsUsed && DateTime.UtcNow <= ExpiryDate;

    // Fabrika metodu - null-conditional operatör eklendi
    public static RefreshToken CreateToken(
        string token,
        string tokenHash,
        string userId,
        string jwtId,
        string createdByIp,
        string userAgent,
        DateTime expiryDate,
        string? familyId = null)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid().ToString(), // Entity sınıfı Id'yi otomatik oluşturmazsa
            TokenHash = tokenHash,
            UserId = userId,
            JwtId = jwtId,
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = expiryDate,
            CreatedByIp = createdByIp,
            UserAgent = userAgent,
            IsRevoked = false,
            IsUsed = false,
            FamilyId = familyId,
            ReasonRevoked = null,
            RevokedDate = null,
            RevokedByIp = null
        };

        // Token'ı private alana ayarlamak için SetToken metodunu çağır
        refreshToken.SetToken(token);

        return refreshToken;
    }

    // Token değerini ayarlamak için yardımcı metot eklendi
    public void SetToken(string token)
    {
        Token = token;
    }

    // Base constructor çağrısı
    public RefreshToken() : base("RefreshToken")
    {
    }
}