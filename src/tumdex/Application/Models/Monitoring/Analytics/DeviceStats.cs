namespace Application.Models.Monitoring.Analytics;

public class DeviceStats
{
    // <summary>
    // Masaüstü cihazlardan gelen oturum sayısı
    // </summary>
    public int Desktop { get; set; }
    
    // <summary>
    // Mobil cihazlardan gelen oturum sayısı
    // </summary>
    public int Mobile { get; set; }
    
    // <summary>
    // Tablet cihazlardan gelen oturum sayısı
    // </summary>
    public int Tablet { get; set; }
    
    // <summary>
    // Tarayıcı dağılımı (tarayıcı adı -> oturum sayısı)
    // </summary>
    public Dictionary<string, int> Browsers { get; set; } = new Dictionary<string, int>();
}