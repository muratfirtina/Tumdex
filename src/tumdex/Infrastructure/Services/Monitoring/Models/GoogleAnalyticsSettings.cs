namespace Infrastructure.Services.Monitoring.Models;

/// <summary>
/// Google Analytics API erişimi için gerekli yapılandırma ayarları
/// </summary>
public class GoogleAnalyticsSettings
{
    /// <summary>
    /// Google Analytics View ID'si. "ga:XXXXXX" formatında olmalıdır.
    /// </summary>
    public string ViewId { get; set; }
    
    /// <summary>
    /// Google Cloud Console'dan indirilen service account kimlik bilgileri JSON dosyasının yolu
    /// </summary>
    public string KeyFilePath { get; set; }
    
    /// <summary>
    /// Google Analytics API isteklerinde kullanılacak uygulama adı
    /// </summary>
    public string ApplicationName { get; set; } = "TUMDEX Analytics";
    
    /// <summary>
    /// API isteklerinde kullanılacak özel ölçüm boyutları (dimensions)
    /// </summary>
    public List<string> CustomDimensions { get; set; } = new List<string>();
    
    /// <summary>
    /// API isteklerinde kullanılacak özel ölçüm metrikleri (metrics)
    /// </summary>
    public List<string> CustomMetrics { get; set; } = new List<string>();
    
    /// <summary>
    /// API istek önbelleğinin etkin olup olmadığı
    /// </summary>
    public bool EnableCaching { get; set; } = true;
    
    /// <summary>
    /// Önbellek süresi (saat cinsinden)
    /// </summary>
    public int CacheHours { get; set; } = 6;
    
    /// <summary>
    /// API isteklerinde kullanılacak maksimum sonuç sayısı
    /// </summary>
    public int MaxResults { get; set; } = 1000;
    
    /// <summary>
    /// Yapılandırmanın geçerli olup olmadığını kontrol eder
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(ViewId) && !string.IsNullOrEmpty(KeyFilePath);
}