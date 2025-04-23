using Application.Models.Monitoring.Analytics;
using Application.Services;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Infrastructure.Services.Monitoring.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Security.KeyVault.Secrets;

namespace Infrastructure.Services.Monitoring;

/// <summary>
/// Google Analytics API'sini kullanarak veri çeken servis sınıfı
/// </summary>
public class GoogleAnalyticsService : IGoogleAnalyticsService
{
    private readonly ILogger<GoogleAnalyticsService> _logger;
    private readonly GoogleAnalyticsSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly SecretClient _secretClient;
    private AnalyticsService _analyticsService;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
    private string _cachedViewId;
    private string _cachedKeyFilePath;
    private readonly SemaphoreSlim _viewIdLock = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _keyFilePathLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// GoogleAnalyticsService sınıfının yapıcı metodu
    /// </summary>
    public GoogleAnalyticsService(
        IOptions<GoogleAnalyticsSettings> settings,
        IMemoryCache cache,
        SecretClient secretClient,
        IConfiguration configuration,
        ILogger<GoogleAnalyticsService> logger)
    {
        _settings = settings.Value;
        _cache = cache;
        _secretClient = secretClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Google Analytics View ID'sini Key Vault'tan veya yapılandırmadan alır
    /// </summary>
    private async Task<string> GetViewIdAsync()
    {
        if (!string.IsNullOrEmpty(_cachedViewId))
            return _cachedViewId;

        await _viewIdLock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_cachedViewId))
                return _cachedViewId;

            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            try
            {
                // Key Vault'tan veya yapılandırmadan değerleri al
                if (_secretClient != null && !isDevelopment)
                {
                    try
                    {
                        // ViewId Key Vault'tan al
                        var viewIdSecret = await _secretClient.GetSecretAsync("GoogleAnalyticsViewId");
                        _cachedViewId = viewIdSecret.Value.Value;
                        _logger.LogInformation("Google Analytics ViewId Key Vault'tan alındı");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "GoogleAnalyticsViewId Key Vault'tan alınamadı, yapılandırma dosyasındaki değer kullanılacak");
                        _cachedViewId = _settings.ViewId;
                    }
                }
                else
                {
                    // Development ortamında appsettings'ten al
                    _cachedViewId = _settings.ViewId;
                    _logger.LogInformation("Google Analytics ViewId yapılandırma dosyasından alındı");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Analytics ViewId alınamadı");
                _cachedViewId = _settings.ViewId;
            }

            return _cachedViewId;
        }
        finally
        {
            _viewIdLock.Release();
        }
    }

    /// <summary>
    /// Google Analytics API erişimi için kullanılacak JSON anahtar dosya yolunu alır
    /// </summary>
    private async Task<string> GetKeyFilePathAsync()
    {
        if (!string.IsNullOrEmpty(_cachedKeyFilePath))
            return _cachedKeyFilePath;

        await _keyFilePathLock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_cachedKeyFilePath))
                return _cachedKeyFilePath;

            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            try
            {
                // Key Vault'tan veya yapılandırmadan değerleri al
                if (_secretClient != null && !isDevelopment)
                {
                    try
                    {
                        // KeyFilePath Key Vault'tan al
                        var keyFilePathSecret = await _secretClient.GetSecretAsync("GoogleStorageCredentialsPath");
                        _cachedKeyFilePath = keyFilePathSecret.Value.Value;
                        _logger.LogInformation("Google Analytics KeyFilePath Key Vault'tan alındı");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "GoogleStorageCredentialsPath Key Vault'tan alınamadı, yapılandırma dosyasındaki değer kullanılacak");
                        _cachedKeyFilePath = _settings.KeyFilePath;
                    }
                }
                else
                {
                    // Development ortamında appsettings'ten al
                    _cachedKeyFilePath = _settings.KeyFilePath;
                    _logger.LogInformation("Google Analytics KeyFilePath yapılandırma dosyasından alındı");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Analytics KeyFilePath alınamadı");
                _cachedKeyFilePath = _settings.KeyFilePath;
            }

            return _cachedKeyFilePath;
        }
        finally
        {
            _keyFilePathLock.Release();
        }
    }

    /// <summary>
    /// Google Analytics API servisini başlatır
    /// </summary>
    private async Task<bool> InitializeServiceAsync()
    {
        if (_isInitialized && _analyticsService != null) return true;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized && _analyticsService != null) return true;

            string viewId = await GetViewIdAsync();
            string keyFilePath = await GetKeyFilePathAsync();

            // ViewId ve KeyFilePath null kontrolü
            if (string.IsNullOrEmpty(viewId) || string.IsNullOrEmpty(keyFilePath))
            {
                _logger.LogWarning("Google Analytics settings are not valid. ViewId or KeyFilePath is missing.");
                return false;
            }

            try
            {
                GoogleCredential credential;
                
                // JSON anahtarı dosyasından kimlik bilgilerini yükle
                using (var stream = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(AnalyticsService.Scope.AnalyticsReadonly);
                }

                // Analytics API servisini oluştur
                _analyticsService = new AnalyticsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = _settings.ApplicationName,
                });

                _isInitialized = true;
                _logger.LogInformation("Google Analytics servisi başarıyla başlatıldı");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Analytics servisi başlatılırken hata oluştu");
                return false;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Google Analytics API servisinin kullanılabilir olup olmadığını kontrol eder
    /// </summary>
    public async Task<bool> IsServiceAvailableAsync()
    {
        if (!await InitializeServiceAsync())
            return false;

        try
        {
            string viewId = await GetViewIdAsync();

            // Test sorgusu yap
            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
                DateTime.UtcNow.ToString("yyyy-MM-dd"),
                "ga:sessions");

            request.MaxResults = 1;
            var response = await request.ExecuteAsync();

            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics API test sorgusu yapılırken hata oluştu");
            return false;
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki ziyaretçi verilerini getirir
    /// </summary>
    public async Task<List<VisitorCountByDate>> GetVisitorsTimelineAsync(DateTime startDate, DateTime endDate)
    {
        if (!await InitializeServiceAsync())
            return new List<VisitorCountByDate>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_visitors_timeline_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out List<VisitorCountByDate> cachedData))
            {
                return cachedData;
            }

            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:users,ga:newUsers,ga:sessions,ga:pageviews");

            request.Dimensions = "ga:date";
            request.Sort = "ga:date";

            var response = await request.ExecuteAsync();

            var result = new List<VisitorCountByDate>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string dateStr = row[0]; // Format: YYYYMMDD
                    DateTime date = DateTime.ParseExact(dateStr, "yyyyMMdd", null);
                    
                    int users = int.Parse(row[1]);
                    int newUsers = int.Parse(row[2]);
                    int sessions = int.Parse(row[3]);
                    int pageviews = int.Parse(row[4]);

                    result.Add(new VisitorCountByDate
                    {
                        Date = date,
                        TotalVisitors = users,
                        NewVisitors = newUsers,
                        Sessions = sessions,
                        PageViews = pageviews
                    });
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten ziyaretçi zaman çizelgesi alınırken hata oluştu");
            return new List<VisitorCountByDate>();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki en çok trafik getiren referrer'ları getirir
    /// </summary>
    public async Task<Dictionary<string, int>> GetTopReferrersAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        if (!await InitializeServiceAsync())
            return new Dictionary<string, int>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_top_referrers_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, int> cachedData))
            {
                return cachedData;
            }

            // Analytics API'sine gönderilecek isteği oluştur
            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            request.Dimensions = "ga:fullReferrer";
            request.Sort = "-ga:sessions";
            request.MaxResults = limit;
            request.Filters = "ga:medium==referral";

            // API'ye istek gönder
            var response = await request.ExecuteAsync();

            // Sonuçları işle
            var result = new Dictionary<string, int>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string referrer = row[0];
                    int sessions = int.Parse(row[1]);
                    result[referrer] = sessions;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten üst referrerlar alınırken hata oluştu");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki en çok trafik getiren kaynakları getirir
    /// </summary>
    public async Task<Dictionary<string, int>> GetTopSourcesAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        if (!await InitializeServiceAsync())
            return new Dictionary<string, int>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_top_sources_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, int> cachedData))
            {
                return cachedData;
            }

            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            request.Dimensions = "ga:source";
            request.Sort = "-ga:sessions";
            request.MaxResults = limit;

            var response = await request.ExecuteAsync();

            var result = new Dictionary<string, int>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string source = row[0];
                    int sessions = int.Parse(row[1]);
                    result[source] = sessions;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten üst kaynaklar alınırken hata oluştu");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki en çok görüntülenen sayfaları getirir
    /// </summary>
    public async Task<Dictionary<string, int>> GetTopPagesAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        if (!await InitializeServiceAsync())
            return new Dictionary<string, int>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_top_pages_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, int> cachedData))
            {
                return cachedData;
            }

            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:pageviews");

            request.Dimensions = "ga:pagePath";
            request.Sort = "-ga:pageviews";
            request.MaxResults = limit;

            var response = await request.ExecuteAsync();

            var result = new Dictionary<string, int>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string page = row[0];
                    int pageviews = int.Parse(row[1]);
                    result[page] = pageviews;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten üst sayfalar alınırken hata oluştu");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki cihaz istatistiklerini getirir
    /// </summary>
    public async Task<DeviceStats> GetDeviceBreakdownAsync(DateTime startDate, DateTime endDate)
    {
        if (!await InitializeServiceAsync())
            return new DeviceStats();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_device_stats_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out DeviceStats cachedData))
            {
                return cachedData;
            }

            // Cihaz kategorisi dağılımı
            var deviceRequest = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            deviceRequest.Dimensions = "ga:deviceCategory";

            var deviceResponse = await deviceRequest.ExecuteAsync();

            var result = new DeviceStats();
            if (deviceResponse.Rows != null)
            {
                foreach (var row in deviceResponse.Rows)
                {
                    string device = row[0].ToLower();
                    int sessions = int.Parse(row[1]);

                    if (device == "desktop")
                        result.Desktop = sessions;
                    else if (device == "mobile")
                        result.Mobile = sessions;
                    else if (device == "tablet")
                        result.Tablet = sessions;
                }
            }

            // Tarayıcı dağılımı için yeni istek
            var browserRequest = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            browserRequest.Dimensions = "ga:browser";
            browserRequest.Sort = "-ga:sessions";
            browserRequest.MaxResults = 5; // En popüler 5 tarayıcı

            var browserResponse = await browserRequest.ExecuteAsync();

            result.Browsers = new Dictionary<string, int>();
            if (browserResponse.Rows != null)
            {
                foreach (var row in browserResponse.Rows)
                {
                    string browser = row[0];
                    int sessions = int.Parse(row[1]);
                    result.Browsers[browser] = sessions;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten cihaz dağılımı alınırken hata oluştu");
            return new DeviceStats();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki tarayıcı dağılımını getirir
    /// </summary>
    public async Task<Dictionary<string, int>> GetBrowserBreakdownAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        if (!await InitializeServiceAsync())
            return new Dictionary<string, int>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_browser_breakdown_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, int> cachedData))
            {
                return cachedData;
            }

            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            request.Dimensions = "ga:browser";
            request.Sort = "-ga:sessions";
            request.MaxResults = limit;

            var response = await request.ExecuteAsync();

            var result = new Dictionary<string, int>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string browser = row[0];
                    int sessions = int.Parse(row[1]);
                    result[browser] = sessions;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten tarayıcı dağılımı alınırken hata oluştu");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki ülke dağılımını getirir
    /// </summary>
    public async Task<Dictionary<string, int>> GetCountryBreakdownAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        if (!await InitializeServiceAsync())
            return new Dictionary<string, int>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı
            string cacheKey = $"ga_country_breakdown_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, int> cachedData))
            {
                return cachedData;
            }

            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            request.Dimensions = "ga:country";
            request.Sort = "-ga:sessions";
            request.MaxResults = limit;

            var response = await request.ExecuteAsync();

            var result = new Dictionary<string, int>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string country = row[0];
                    int sessions = int.Parse(row[1]);
                    result[country] = sessions;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten ülke dağılımı alınırken hata oluştu");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Belirtilen tarih aralığındaki şehir dağılımını getirir
    /// </summary>
    public async Task<Dictionary<string, int>> GetCityBreakdownAsync(DateTime startDate, DateTime endDate, string country = null, int limit = 10)
    {
        if (!await InitializeServiceAsync())
            return new Dictionary<string, int>();

        try
        {
            string viewId = await GetViewIdAsync();
            
            // Önbellek anahtarı (ülke filtresi varsa ekle)
            string cacheKey = $"ga_city_breakdown_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{country ?? "all"}_{limit}";
            
            // Önbellekte varsa getir
            if (_settings.EnableCaching && _cache.TryGetValue(cacheKey, out Dictionary<string, int> cachedData))
            {
                return cachedData;
            }

            var request = _analyticsService.Data.Ga.Get(
                $"ga:{viewId}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                "ga:sessions");

            request.Dimensions = "ga:city";
            request.Sort = "-ga:sessions";
            request.MaxResults = limit;

            // Eğer ülke belirtilmişse filtrele
            if (!string.IsNullOrEmpty(country))
            {
                request.Filters = $"ga:country=={country}";
            }

            var response = await request.ExecuteAsync();

            var result = new Dictionary<string, int>();
            if (response.Rows != null)
            {
                foreach (var row in response.Rows)
                {
                    string city = row[0];
                    int sessions = int.Parse(row[1]);
                    result[city] = sessions;
                }
            }

            // Önbelleğe kaydet
            if (_settings.EnableCaching)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheHours));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Analytics'ten şehir dağılımı alınırken hata oluştu");
            return new Dictionary<string, int>();
        }
    }
}