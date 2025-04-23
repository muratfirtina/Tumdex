using Application.Models.Monitoring.Analytics;

namespace Application.Services;

/// <summary>
/// Google Analytics'ten veri çekmek için kullanılan servis arayüzü
/// </summary>
public interface IGoogleAnalyticsService
{
    /// <summary>
    /// Servisin aktif ve kullanılabilir olup olmadığını kontrol eder
    /// </summary>
    /// <returns>Servis kullanılabilir ise true, değilse false</returns>
    Task<bool> IsServiceAvailableAsync();
    
    /// <summary>
    /// Belirtilen tarih aralığında ziyaretçi ve oturum sayılarını getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <returns>Tarih bazlı ziyaretçi verileri</returns>
    Task<List<VisitorCountByDate>> GetVisitorsTimelineAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// Belirtilen tarih aralığında en çok trafik getiren referrer'ları (yönlendiren siteleri) getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Referrer listesi ve ziyaret sayıları</returns>
    Task<Dictionary<string, int>> GetTopReferrersAsync(DateTime startDate, DateTime endDate, int limit = 10);
    
    /// <summary>
    /// Belirtilen tarih aralığında en çok trafik getiren kaynakları (source) getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Kaynak listesi ve ziyaret sayıları</returns>
    Task<Dictionary<string, int>> GetTopSourcesAsync(DateTime startDate, DateTime endDate, int limit = 10);
    
    /// <summary>
    /// Belirtilen tarih aralığında en çok görüntülenen sayfaları getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Sayfa listesi ve görüntülenme sayıları</returns>
    Task<Dictionary<string, int>> GetTopPagesAsync(DateTime startDate, DateTime endDate, int limit = 10);
    
    /// <summary>
    /// Belirtilen tarih aralığında cihaz tipine göre oturum dağılımını getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <returns>Cihaz dağılımı istatistikleri</returns>
    Task<DeviceStats> GetDeviceBreakdownAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// Belirtilen tarih aralığında tarayıcı tipine göre oturum dağılımını getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Tarayıcı dağılımı</returns>
    Task<Dictionary<string, int>> GetBrowserBreakdownAsync(DateTime startDate, DateTime endDate, int limit = 10);
    
    /// <summary>
    /// Belirtilen tarih aralığında ülke bazlı oturum dağılımını getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Ülke dağılımı</returns>
    Task<Dictionary<string, int>> GetCountryBreakdownAsync(DateTime startDate, DateTime endDate, int limit = 10);
    
    /// <summary>
    /// Belirtilen tarih aralığında şehir bazlı oturum dağılımını getirir
    /// </summary>
    /// <param name="startDate">Başlangıç tarihi</param>
    /// <param name="endDate">Bitiş tarihi</param>
    /// <param name="country">Opsiyonel olarak belirli bir ülke için filtreleme yapar</param>
    /// <param name="limit">Maksimum sonuç sayısı</param>
    /// <returns>Şehir dağılımı</returns>
    Task<Dictionary<string, int>> GetCityBreakdownAsync(DateTime startDate, DateTime endDate, string country = null, int limit = 10);
}