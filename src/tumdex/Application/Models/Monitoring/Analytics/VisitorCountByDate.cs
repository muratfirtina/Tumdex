namespace Application.Models.Monitoring.Analytics;

public class VisitorCountByDate
{
    // <summary>
    // Tarih
    // </summary>
    public DateTime Date { get; set; }
    
    // <summary>
    // Toplam tekil ziyaretçi sayısı
    // </summary>
    public int TotalVisitors { get; set; }
    
    // <summary>
    // Yeni ziyaretçi sayısı (ilk kez siteyi ziyaret edenler)
    // </summary>
    public int NewVisitors { get; set; }
    
    // <summary>
    // Oturum sayısı
    // </summary>
    public int Sessions { get; set; }
    
    // <summary>
    // Sayfa görüntüleme sayısı
    // </summary>
    public int PageViews { get; set; }
}