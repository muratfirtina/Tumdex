namespace Application.Models.Monitoring;

public class VisitorTracking
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public string CurrentPage { get; set; } = "/";
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public string ConnectionId { get; set; } // SignalR bağlantı kimliği
    public string Referrer { get; set; }  // Trafik kaynağı (Google, Yandex, Instagram, vb.)
    public string Page { get; set; }      // Ziyaret edilen sayfa
    public DateTime VisitTime { get; set; } = DateTime.UtcNow; // Ziyaret zamanı
    public string Country { get; set; }
    public string City { get; set; }
    public string DeviceType { get; set; } // Mobil, Masaüstü, Tablet
    public string BrowserName { get; set; }
    public bool IsNewVisitor { get; set; } // İlk ziyaret mi?
    public string SessionId { get; set; } // Tarayıcı oturumunu takip etmek için
}