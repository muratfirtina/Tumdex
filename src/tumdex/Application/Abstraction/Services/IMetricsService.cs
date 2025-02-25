namespace Application.Abstraction.Services;

public interface IMetricsService
{
    #region Existing Metrics
    
    /// <summary>
    /// Rate limit aşımlarını kaydetmek için kullanılır
    /// </summary>
    void IncrementRateLimitHit(string clientIp, string path, string userId = "anonymous");

    /// <summary>
    /// DDoS şüphesi olan istekleri kaydetmek için kullanılır
    /// </summary>
    void IncrementDdosAttempt(string clientIp, string path);

    /// <summary>
    /// İstek sürelerini kaydetmek için kullanılır
    /// </summary>
    void RecordRequestDuration(string method, string endpoint, double durationMs);

    /// <summary>
    /// Cache hit olaylarını sayar
    /// </summary>
    void IncrementCacheHit(string cacheKey, string cacheType = "redis");

    /// <summary>
    /// Cache miss olaylarını sayar
    /// </summary>
    void IncrementCacheMiss(string cacheKey, string cacheType = "redis");

    /// <summary>
    /// Alert sayılarını kaydetmek için kullanılır
    /// </summary>
    void IncrementAlertCounter(string alertType, Dictionary<string, string> labels);

    /// <summary>
    /// Toplam istek sayısını kaydetmek için kullanılır
    /// </summary>
    void IncrementTotalRequests(string method, string endpoint, string statusCode);

    /// <summary>
    /// Aktif bağlantı sayısını takip etmek için kullanılır
    /// </summary>
    void TrackActiveConnection(string type, int delta = 1);

    /// <summary>
    /// Güvenlik olaylarını kaydetmek için kullanılır
    /// </summary>
    void RecordSecurityEvent(string eventType, string severity);

    #endregion

    #region Payment Metrics

    /// <summary>
    /// Ödeme işlem sürelerini kaydetmek için kullanılır
    /// </summary>
    void RecordPaymentDuration(string provider, string type, double duration);

    /// <summary>
    /// Ödeme işlemlerinin sayısını kaydetmek için kullanılır
    /// </summary>
    void IncrementPaymentTransactions(string status, string provider, string type);

    /// <summary>
    /// Başarısız ödeme işlemlerini kaydetmek için kullanılır
    /// </summary>
    void IncrementFailedPayments(string reason, string provider);

    #endregion

    #region Cart and Order Metrics

    /// <summary>
    /// Sepet terk oranını güncellemek için kullanılır
    /// </summary>
    void UpdateCartAbandonment(string userType, double rate);

    /// <summary>
    /// Tamamlanan sipariş sayısını artırmak için kullanılır
    /// </summary>
    void IncrementOrderCompletion(string orderType, string paymentMethod);

    /// <summary>
    /// Ödeme sürecinin süresini kaydetmek için kullanılır
    /// </summary>
    void RecordCheckoutDuration(string userType, string paymentMethod, double duration);

    #endregion

    #region API Usage Metrics

    /// <summary>
    /// API endpoint çağrılarını kaydetmek için kullanılır
    /// </summary>
    void RecordApiCall(string endpoint, string method, string version);

    /// <summary>
    /// API endpoint gecikme sürelerini kaydetmek için kullanılır
    /// </summary>
    void RecordApiLatency(string endpoint, string method, double duration);

    #endregion

    #region Session Metrics

    /// <summary>
    /// Aktif kullanıcı sayısını güncellemek için kullanılır
    /// </summary>
    void UpdateActiveUsers(string userType, int count);

    /// <summary>
    /// Kullanıcı girişlerini kaydetmek için kullanılır
    /// </summary>
    void IncrementUserLogins(string authMethod, string userType);

    /// <summary>
    /// Oturum sürelerini kaydetmek için kullanılır
    /// </summary>
    void RecordSessionDuration(string userType, double duration);

    #endregion
    
    #region Auth Metrics

    /// <summary>
    /// Başarısız login denemelerini kaydetmek için kullanılır
    /// </summary>
    void IncrementFailedLogins(string reason, string userType = "anonymous");

    /// <summary>
    /// Token yenileme işlemlerini kaydetmek için kullanılır
    /// </summary>
    void RecordTokenRefresh(string tokenType, bool success);

    /// <summary>
    /// Yetkilendirme kararlarını kaydetmek için kullanılır
    /// </summary>
    void RecordAuthorizationDecision(string endpoint, string role, bool allowed);

    /// <summary>
    /// Password reset işlemlerini kaydetmek için kullanılır
    /// </summary>
    void RecordPasswordReset(string initiationType, bool success);

    #endregion
}