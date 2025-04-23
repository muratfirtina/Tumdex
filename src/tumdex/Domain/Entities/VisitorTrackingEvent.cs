using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain.Entities;

public class VisitorTrackingEvent : Entity<string>
{
    public string SessionId { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public string Page { get; set; }
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; }
    public DateTime VisitTime { get; set; }

    // Referrer bilgileri
    public string Referrer { get; set; }
    public string ReferrerDomain { get; set; }
    public string ReferrerType { get; set; }

    // UTM parametreleri
    public string UTMSource { get; set; }
    public string UTMMedium { get; set; }
    public string UTMCampaign { get; set; }

    // Konum bilgileri
    public string Country { get; set; }
    public string City { get; set; }

    // Cihaz bilgileri
    public string DeviceType { get; set; }
    public string BrowserName { get; set; }
    public bool IsNewVisitor { get; set; }

    // Kullanıcı referansı (opsiyonel)
    public string UserId { get; set; }
    public AppUser User { get; set; }

    // Constructorlar
    public VisitorTrackingEvent()
    {
        Id = Guid.NewGuid().ToString("N");
        VisitTime = DateTime.UtcNow;

        // Null olmaması gereken alanlara varsayılan değerler
        ReferrerDomain = string.Empty;
        ReferrerType = "Doğrudan";
        Referrer = string.Empty;
        UTMSource = string.Empty;
        UTMMedium = string.Empty;
        UTMCampaign = string.Empty;
        Username = "Anonim";
        DeviceType = "Bilinmiyor";
        BrowserName = "Bilinmiyor";
    }

    public VisitorTrackingEvent(
        string sessionId,
        string ipAddress,
        string userAgent,
        string page,
        bool isAuthenticated = false,
        string username = "Anonim",
        string referrer = "",
        string deviceType = "Bilinmiyor",
        string browserName = "Bilinmiyor")
    {
        Id = Guid.NewGuid().ToString("N");
        SessionId = sessionId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Page = page;
        IsAuthenticated = isAuthenticated;
        Username = username ?? "Anonim"; // Null check ekle
        VisitTime = DateTime.UtcNow;
        Referrer = referrer ?? string.Empty; // Null check ekle
        DeviceType = deviceType ?? "Bilinmiyor"; // Null check ekle
        BrowserName = browserName ?? "Bilinmiyor"; // Null check ekle

        // Referrer domain ve tipini çıkar - Null kontrolü ekle
        ReferrerDomain = string.Empty; // Varsayılan değer
        ReferrerType = "Doğrudan"; // Varsayılan değer

        if (!string.IsNullOrEmpty(referrer))
        {
            try
            {
                var uri = new Uri(referrer);
                ReferrerDomain = uri.Host;
                ReferrerType = DetermineReferrerType(ReferrerDomain);
            }
            catch
            {
                ReferrerDomain = referrer;
                ReferrerType = "Diğer";
            }
        }

        // Boş değer olmaması gereken diğer alanlar
        UTMSource = string.Empty;
        UTMMedium = string.Empty;
        UTMCampaign = string.Empty;
    }

    // Yardımcı metodlar
    private string DetermineReferrerType(string referrerDomain)
    {
        if (string.IsNullOrEmpty(referrerDomain))
            return "Doğrudan";

        // Arama motorları
        if (referrerDomain.Contains("google.") ||
            referrerDomain.Contains("bing.") ||
            referrerDomain.Contains("yahoo.") ||
            referrerDomain.Contains("yandex."))
            return "Arama Motoru";

        // Sosyal medya
        if (referrerDomain.Contains("facebook.") ||
            referrerDomain.Contains("instagram.") ||
            referrerDomain.Contains("twitter.") ||
            referrerDomain.Contains("linkedin.") ||
            referrerDomain.Contains("t.co") ||
            referrerDomain.Contains("youtube.") ||
            referrerDomain.Contains("pinterest."))
            return "Sosyal Medya";

        // E-posta servisleri
        if (referrerDomain.Contains("mail.") ||
            referrerDomain.Contains("gmail.") ||
            referrerDomain.Contains("outlook.") ||
            referrerDomain.Contains("yahoo."))
            return "E-posta";

        return "Diğer";
    }
}