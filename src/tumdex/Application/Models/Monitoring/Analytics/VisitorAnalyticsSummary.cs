namespace Application.Models.Monitoring.Analytics;

public class VisitorAnalyticsSummary
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalVisits { get; set; }
    public int UniqueVisitors { get; set; }
    public int AuthenticatedUsers { get; set; }
    public int AnonymousUsers { get; set; }
    public int NewVisitors { get; set; }
    public int ReturningVisitors { get; set; }
    
    // Cihaz dağılımı
    public Dictionary<string, int> DeviceBreakdown { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> BrowserBreakdown { get; set; } = new Dictionary<string, int>();
    
    // Trafik kaynakları
    public Dictionary<string, int> ReferrerTypeBreakdown { get; set; } = new Dictionary<string, int>();
    
    // Saatlik analiz
    public Dictionary<int, int> HourlyBreakdown { get; set; } = new Dictionary<int, int>();
    
    // Günlük analiz (tarih aralığı sorgulandığında)
    public Dictionary<DateTime, int> DailyBreakdown { get; set; } = new Dictionary<DateTime, int>();
}