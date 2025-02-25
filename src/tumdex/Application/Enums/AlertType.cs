namespace Application.Enums;

public enum AlertType
{
    RateLimit,          // Rate limit aşımı
    DDoS,              // DDoS şüphesi
    HighLatency,       // Yüksek gecikme
    CacheFailure,      // Cache hatası
    SecurityThreat,    // Güvenlik tehdidi
    DatabaseError,     // Veritabanı hatası
    SystemError,       // Sistem hatası
    ServiceDown,       // Servis kesintisi
    CriticalError,     // Kritik hata
    Warning,           // Uyarı
    Info              // Bilgilendirme
}