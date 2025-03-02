using Core.Persistence.Repositories;

namespace Domain.Identity;

public class RefreshToken : Entity<string>
{
    // Token hash'i veritabanında saklanır
    public string TokenHash { get; set; }
        
    // Orijinal token client'a gönderilir ve sadece oluşturma sırasında kullanılır
    // Bu alan veritabanında saklanmaz
    public string Token { get; private set; }
        
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
    
    // İptal bilgileri - Eksik olan özellikler
    public string? RevokedByIp { get; set; }
    public string? ReasonRevoked { get; set; }
    public DateTime? RevokedDate { get; set; }
        
    // Token aktif mi kontrolü
    public bool IsActive => !IsRevoked && !IsUsed && DateTime.UtcNow <= ExpiryDate;
        
    // Fabrika metodu
    public static RefreshToken CreateToken(
        string token,
        string tokenHash,
        string userId,
        string jwtId,
        string createdByIp,
        string userAgent,
        DateTime expiryDate,
        string familyId = null)
    {
        return new RefreshToken
        {
            Token = token, // Bu alan client'a gönderilir, veritabanında saklanmaz
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
    }
    public RefreshToken() : base("RefreshToken")
    {
        
    }
}