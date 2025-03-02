using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Security.Models;

public class JwtSettings
    {
        public bool ValidateIssuerSigningKey { get; set; } = true;
        public bool ValidateIssuer { get; set; } = true;
        public bool ValidateAudience { get; set; } = true;
        public bool ValidateLifetime { get; set; } = true;
        public int ClockSkewMinutes { get; set; } = 5;
        
        // Token süreleri
        public int AccessTokenLifetimeMinutes { get; set; } = 30; // Kısa ömürlü access token (30 dk)
        public int RefreshTokenLifetimeDays { get; set; } = 14;  // 2 haftalık refresh token
        
        // Token ailelendirme
        public bool UseTokenFamilies { get; set; } = true;
        
        // Güvenlik özellikleri
        public bool RotateRefreshTokens { get; set; } = true;   // Her kullanımda yeni refresh token
        public bool CheckIpAddress { get; set; } = true;        // IP kontrolü
        public bool CheckUserAgent { get; set; } = true;        // Tarayıcı kontrolü
        
        // Birden fazla cihazda oturum açmayı sınırlama (isteğe bağlı)
        public int MaxActiveRefreshTokens { get; set; } = 5;    // Kullanıcı başına max token sayısı
        
        // Email doğrulama ayarı
        public bool RequireEmailConfirmation { get; set; } = true;
    }

