using System.Diagnostics;
using Application.Abstraction.Services.Utilities; // IMetricsService arayüzü için
using Prometheus;
using System; // DateTime, Random, Math, GC için
using System.Collections.Generic; // Dictionary, IEnumerable için
using System.Linq; // GetValueOrDefault için (opsiyonel, elle de yapılabilir)

namespace Infrastructure.Services.Monitoring
{
    public class PrometheusMetricsService : IMetricsService
    {
        // --- Metrik Alanları ---

        #region Genel Metrikler
        private readonly Counter _totalRequests;
        private readonly Counter _errorRequests; // Hatalı istekler (genel)
        private readonly Histogram _requestDuration;
        private readonly Counter _securityEvents;
        private readonly Gauge _activeConnections;
        private readonly Counter _rateLimitHits;
        private readonly Counter _ddosAttempts;
        private readonly Counter _cacheHits;
        private readonly Counter _cacheMisses;
        private readonly Counter _alertCounter;
        private readonly Counter _generalCounter; // Çeşitli operasyonlar için genel sayaç
        #endregion

        #region E-Ticaret Metrikleri
        private readonly Histogram _paymentProcessingDuration;
        private readonly Counter _paymentTransactions;
        private readonly Counter _failedPayments;
        private readonly Gauge _cartAbandonment;
        private readonly Counter _orderCompletions;
        private readonly Counter _checkoutStarts;
        private readonly Histogram _checkoutDuration;
        #endregion

        #region API Kullanım Metrikleri
        private readonly Counter _apiEndpointCalls;
        private readonly Histogram _apiEndpointLatency;
        private readonly Counter _apiErrors; // API özelinde hatalar (YENİ EKLENDİ)
        #endregion

        #region Oturum Metrikleri
        private readonly Gauge _activeUsers;
        private readonly Counter _userLogins;
        private readonly Counter _failedLoginAttempts; // Başarısız giriş denemeleri (ÖNCEKİ _failedLogins YERİNE)
        private readonly Histogram _sessionDuration;
        #endregion

        #region Kimlik Doğrulama (Auth) Metrikleri
        // _failedLoginAttempts yukarıda zaten tanımlı
        private readonly Counter _tokenRefreshes;
        private readonly Counter _authorizationDecisions;
        private readonly Counter _passwordResets;
        private readonly Histogram _authDuration;
        #endregion

        // --- Constructor ---
        // Constructor artık parametre almıyor, tüm metrikler burada oluşturulacak.
        public PrometheusMetricsService()
        {
            // --- Metriklerin Oluşturulması ---

            // Genel Metrikler
            _generalCounter = Metrics.CreateCounter(
                "general_counter",
                "General counter for various operations",
                new CounterConfiguration { LabelNames = new[] { "type", "status" } }
            );

            _totalRequests = Metrics.CreateCounter(
                "requests_total",
                "Total number of HTTP requests",
                new CounterConfiguration { LabelNames = new[] { "method", "endpoint", "status_code" } }
            );

            _errorRequests = Metrics.CreateCounter(
                "request_errors_total",
                "Total number of HTTP request errors",
                new CounterConfiguration { LabelNames = new[] { "method", "endpoint", "error_type" } }
            );

            _requestDuration = Metrics.CreateHistogram(
                "request_duration_milliseconds",
                "Duration of HTTP requests in milliseconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" },
                    Buckets = Histogram.ExponentialBuckets(50, 2, 10) // Örnek: 50ms'den başlayıp 2 kat artan 10 kova
                    // Buckets = new[] { 0.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0, 5000.0 } // Veya önceki gibi
                }
            );

            _activeConnections = Metrics.CreateGauge(
                "active_connections_total",
                "Number of active connections",
                new GaugeConfiguration { LabelNames = new[] { "type" } }
            );

            _rateLimitHits = Metrics.CreateCounter(
                "rate_limit_hits_total",
                "Number of rate limit hits",
                new CounterConfiguration { LabelNames = new[] { "client_ip", "path", "user_id" } }
            );

            _ddosAttempts = Metrics.CreateCounter(
                "ddos_attempts_total",
                "Number of potential DDoS attempts",
                new CounterConfiguration { LabelNames = new[] { "client_ip", "path" } }
            );

            _securityEvents = Metrics.CreateCounter(
                "security_events_total",
                "Total number of security events",
                new CounterConfiguration { LabelNames = new[] { "event_type", "severity" } }
            );

            _alertCounter = Metrics.CreateCounter(
                "application_alerts_total",
                "Total number of alerts triggered",
                new CounterConfiguration { LabelNames = new[] { "type", "severity" } }
            );

            _cacheHits = Metrics.CreateCounter(
                "cache_hits_total",
                "Total number of cache hits",
                new CounterConfiguration { LabelNames = new[] { "cache_key", "cache_type" } }
            );

            _cacheMisses = Metrics.CreateCounter(
                "cache_misses_total",
                "Total number of cache misses",
                new CounterConfiguration { LabelNames = new[] { "cache_key", "cache_type" } }
            );

            // E-Ticaret Metrikleri
            _paymentProcessingDuration = Metrics.CreateHistogram(
                "payment_processing_duration_seconds",
                "Duration of payment processing",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "payment_provider", "payment_type" },
                    Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 }
                }
            );

            _paymentTransactions = Metrics.CreateCounter(
                "payment_transactions_total",
                "Total number of payment transactions",
                new CounterConfiguration { LabelNames = new[] { "status", "payment_provider", "payment_type" } }
            );

            _failedPayments = Metrics.CreateCounter(
                "failed_payments_total",
                "Total number of failed payments",
                new CounterConfiguration { LabelNames = new[] { "failure_reason", "payment_provider" } }
            );

            _cartAbandonment = Metrics.CreateGauge(
                "cart_abandonment_rate",
                "Current cart abandonment rate as a percentage",
                new GaugeConfiguration { LabelNames = new[] { "user_type" } }
            );

            _orderCompletions = Metrics.CreateCounter(
                "order_completions_total",
                "Total number of completed orders",
                new CounterConfiguration { LabelNames = new[] { "order_type", "payment_method" } }
            );

            _checkoutStarts = Metrics.CreateCounter(
                "checkout_starts_total",
                "Total number of checkout processes started",
                new CounterConfiguration { LabelNames = new[] { "user_type" } }
            );

            _checkoutDuration = Metrics.CreateHistogram(
                "checkout_duration_seconds",
                "Duration of checkout process",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "user_type", "payment_method" },
                    Buckets = new[] { 30.0, 60.0, 120.0, 300.0, 600.0 }
                }
            );

            // API Kullanım Metrikleri
            _apiEndpointCalls = Metrics.CreateCounter(
                "api_endpoint_calls_total",
                "Total number of API endpoint calls",
                new CounterConfiguration { LabelNames = new[] { "endpoint", "method", "version" } }
            );

            _apiEndpointLatency = Metrics.CreateHistogram(
                "api_endpoint_latency_seconds",
                "API endpoint latency in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "endpoint", "method" },
                    Buckets = new[] { 0.1, 0.3, 0.5, 0.7, 1.0, 2.0, 5.0 }
                }
            );

            // DI Hatasına neden olan _apiErrors metriği burada oluşturuluyor
            _apiErrors = Metrics.CreateCounter(
                "api_errors_total", // Örnek isim, isteğe bağlı değiştirilebilir
                "Total number of API specific errors",
                new CounterConfiguration { LabelNames = new[] { "endpoint", "method", "error_code" } } // Örnek etiketler
            );


            // Oturum Metrikleri
            _activeUsers = Metrics.CreateGauge(
                "active_users_total",
                "Number of currently active users",
                new GaugeConfiguration { LabelNames = new[] { "user_type" } }
            );

            _userLogins = Metrics.CreateCounter(
                "user_logins_total",
                "Total number of user logins",
                new CounterConfiguration { LabelNames = new[] { "auth_method", "user_type" } }
            );

            // DI Hatasına neden olan _failedLogins yerine bu kullanılıyor
             _failedLoginAttempts = Metrics.CreateCounter(
                 "failed_login_attempts_total",
                 "Total number of failed login attempts",
                 new CounterConfiguration { LabelNames = new[] { "reason", "user_type" } }
             );

            _sessionDuration = Metrics.CreateHistogram(
                "session_duration_minutes",
                "Duration of user sessions",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "user_type" },
                    Buckets = new[] { 1.0, 5.0, 15.0, 30.0, 60.0, 120.0 }
                }
            );

            // Kimlik Doğrulama (Auth) Metrikleri
            _tokenRefreshes = Metrics.CreateCounter(
                "token_refreshes_total",
                "Total number of token refresh operations",
                new CounterConfiguration { LabelNames = new[] { "token_type", "status" } }
            );

            _authorizationDecisions = Metrics.CreateCounter(
                "authorization_decisions_total",
                "Total number of authorization decisions",
                new CounterConfiguration { LabelNames = new[] { "endpoint", "role", "decision" } }
            );

            _passwordResets = Metrics.CreateCounter(
                "password_resets_total",
                "Total number of password reset operations",
                new CounterConfiguration { LabelNames = new[] { "initiation_type", "status" } }
            );

            _authDuration = Metrics.CreateHistogram(
                "auth_operation_duration_seconds",
                "Duration of authentication operations",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "operation_type" },
                    Buckets = new[] { 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
                }
            );
        }

        // --- Metotlar ---

        #region Genel Metrik Metotları
        public void IncrementTotalRequests(string method, string endpoint, string statusCode) =>
            _totalRequests.WithLabels(method, endpoint, statusCode).Inc();

        public void IncrementRequestErrors(string method, string endpoint, string errorType) =>
             _errorRequests.WithLabels(method, endpoint, errorType).Inc();

        public void RecordRequestDuration(string method, string endpoint, double durationMs) =>
            _requestDuration.WithLabels(method, endpoint).Observe(durationMs);

        public void TrackActiveConnection(string type, int delta)
        {
            type ??= "unknown"; // Null ise "unknown" ata
            if (delta > 0) _activeConnections.WithLabels(type).Inc(delta);
            else _activeConnections.WithLabels(type).Dec(Math.Abs(delta));
        }

         public void IncrementRateLimitHit(string clientIp, string path, string userId = "anonymous") =>
            _rateLimitHits.WithLabels(clientIp, path, userId).Inc();

        public void IncrementDdosAttempt(string clientIp, string path) =>
             _ddosAttempts.WithLabels(clientIp, path).Inc();

        public void RecordSecurityEvent(string eventType, string severity) =>
            _securityEvents.WithLabels(eventType, severity).Inc();

         public void IncrementCacheHit(string cacheKey, string cacheType = "redis")
        {
            try { _cacheHits?.WithLabels(cacheKey, cacheType).Inc(); }
            catch (Exception ex) { Console.WriteLine($"Error recording cache hit metric: {ex.Message}"); }
        }

        public void IncrementCacheMiss(string cacheKey, string cacheType = "redis")
        {
            try { _cacheMisses?.WithLabels(cacheKey, cacheType).Inc(); }
            catch (Exception ex) { Console.WriteLine($"Error recording cache miss metric: {ex.Message}"); }
        }

        public void IncrementAlertCounter(string alertType, Dictionary<string, string> labels)
        {
            try
            {
                alertType ??= "unknown";
                var severity = labels?.GetValueOrDefault("severity", "info") ?? "info";
                _alertCounter.WithLabels(alertType, severity).Inc();
            }
            catch (Exception ex) { Console.WriteLine($"Error incrementing alert counter: {ex.Message}"); }
        }

        public void IncrementCounter(string counterName, string status)
        {
            try { _generalCounter.WithLabels(counterName, status).Inc(); }
            catch (Exception ex) { Console.WriteLine($"Error incrementing counter {counterName}: {ex.Message}"); }
        }
        #endregion

        #region E-Ticaret Metrik Metotları
        public void RecordPaymentDuration(string provider, string type, double duration) =>
            _paymentProcessingDuration.WithLabels(provider, type).Observe(duration);

        public void IncrementPaymentTransactions(string status, string provider, string type) =>
            _paymentTransactions.WithLabels(status, provider, type).Inc();

        public void IncrementFailedPayments(string reason, string provider) =>
            _failedPayments.WithLabels(reason, provider).Inc();

        public void UpdateCartAbandonment(string userType, double rate) =>
            _cartAbandonment.WithLabels(userType).Set(rate);

        public void IncrementOrderCompletion(string orderType, string paymentMethod) =>
            _orderCompletions.WithLabels(orderType, paymentMethod).Inc();

        public void IncrementCheckoutStart(string userType) => // Yeni eklenen metod (opsiyonel)
             _checkoutStarts.WithLabels(userType).Inc();

        public void RecordCheckoutDuration(string userType, string paymentMethod, double duration) =>
            _checkoutDuration.WithLabels(userType, paymentMethod).Observe(duration);
        #endregion

        #region API Kullanım Metrik Metotları
        public void RecordApiCall(string endpoint, string method, string version) =>
            _apiEndpointCalls.WithLabels(endpoint, method, version).Inc();

        public void RecordApiLatency(string endpoint, string method, double duration) =>
            _apiEndpointLatency.WithLabels(endpoint, method).Observe(duration);

        // _apiErrors için artırma metodu
        public void IncrementApiError(string endpoint, string method, string errorCode) =>
             _apiErrors.WithLabels(endpoint, method, errorCode).Inc();
        #endregion

        #region Oturum Metrik Metotları
        public void UpdateActiveUsers(string userType, int count) =>
            _activeUsers.WithLabels(userType).Set(count);

        public void IncrementUserLogins(string authMethod, string userType) =>
            _userLogins.WithLabels(authMethod, userType).Inc();

        // Artık _failedLoginAttempts kullanılıyor
        public void IncrementFailedLogins(string reason, string userType = "anonymous")
        {
            _failedLoginAttempts.WithLabels(reason, userType).Inc();
            RecordSecurityEvent("failed_login", "warning"); // Güvenlik olayı olarak da kaydet
        }

        public void RecordSessionDuration(string userType, double duration) =>
            _sessionDuration.WithLabels(userType).Observe(duration);
        #endregion

        #region Kimlik Doğrulama (Auth) Metrik Metotları
        public void RecordTokenRefresh(string tokenType, bool success)
        {
            var status = success ? "success" : "failure";
            _tokenRefreshes.WithLabels(tokenType, status).Inc();
            if (!success) RecordSecurityEvent("token_refresh_failure", "warning");
        }

        public void RecordAuthorizationDecision(string endpoint, string role, bool allowed)
        {
            var decision = allowed ? "allowed" : "denied";
            _authorizationDecisions.WithLabels(endpoint, role, decision).Inc();
            if (!allowed) RecordSecurityEvent("authorization_denied", "info");
        }

        public void RecordPasswordReset(string initiationType, bool success)
        {
            var status = success ? "success" : "failure";
            _passwordResets.WithLabels(initiationType, status).Inc();
            RecordSecurityEvent("password_reset", success ? "info" : "warning");
        }

        public void RecordAuthOperationDuration(string operationType, double duration)
        {
            _authDuration.WithLabels(operationType).Observe(duration);
            if (duration > 5.0) RecordSecurityEvent("slow_auth_operation", "warning");
        }
        #endregion

        #region Metrik Veri Toplama (Örnek)
        // Bu kısım büyük ölçüde aynı kalabilir, çünkü metriklerin *değerlerini*
        // doğrudan okumak yerine genellikle Prometheus'un scrape endpoint'i kullanılır.
        // Ancak bir özet veya anlık durum sunmak isterseniz bu metotlar kalabilir.

        public object GetCurrentMetrics()
        {
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();
            var activeUsersCount = GetActiveUserCount(); // Örnek, gerçek uygulamada farklı alınabilir

            return new
            {
                systemMetrics = new
                {
                    cpuUsage = cpuUsage,
                    memoryUsage = memoryUsage,
                    activeUsers = activeUsersCount,
                    // Not: Bu hesaplamalar anlık örneklerdir, gerçek değerler için
                    // Prometheus sorguları daha doğru olacaktır.
                    errorRate = CalculateErrorRate(),
                    requestsPerMinute = CalculateRequestsPerMinute(),
                    avgResponseTime = CalculateAverageResponseTime()
                },
                // Endpoint metriklerini Prometheus'tan sorgulamak daha iyi olur.
                // endpoints = GetEndpointMetrics()
            };
        }

        // Yardımcı metotlar (Örnek değerler üretirler)
        private double GetCpuUsage()
        {
             try
            {
                // Platforma bağlı çalışmayabilir, daha robust bir yöntem gerekebilir.
                using var process = Process.GetCurrentProcess();
                // Bu hesaplama basit bir yaklaşımdır, gerçek sistem yükünü tam yansıtmayabilir.
                // Performans sayaçları (Windows) veya /proc (Linux) daha doğru olabilir.
                 TimeSpan startCpuTime = process.TotalProcessorTime;
                 Stopwatch sw = Stopwatch.StartNew();
                 System.Threading.Thread.Sleep(50); // Kısa bir süre bekle
                 sw.Stop();
                 TimeSpan endCpuTime = process.TotalProcessorTime;
                 double cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
                 double totalMsPassed = sw.ElapsedMilliseconds;
                 double cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                 return Math.Min(100, Math.Max(0, cpuUsageTotal * 100));
            }
            catch { return 50; } // Hata durumunda varsayılan
        }

        private double GetMemoryUsage()
        {
             try
            {
                 using var process = Process.GetCurrentProcess();
                 var usedMemoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
                 // .NET 5+ için daha iyi bir toplam bellek bilgisi alınabilir
                 long totalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                 double totalMemoryMB = totalMemoryBytes > 0 ? totalMemoryBytes / (1024.0 * 1024.0) : 8192; // Varsayılan 8GB
                 return totalMemoryMB > 0 ? Math.Min(100, (usedMemoryMB * 100.0 / totalMemoryMB)) : 0;
            }
            catch { return 40; } // Hata durumunda varsayılan
        }

        private int GetActiveUserCount()
        {
             // Gerçek uygulamada bu bilgi oturum yönetiminden veya benzeri bir yerden gelmeli
             // Örneğin, _activeUsers gauge'ının anlık değeri okunabilir (ancak thread-safety dikkat!)
            try { return (int)_activeUsers.Value; } // Basit bir okuma (etiketsiz varsayılıyor)
            catch { return new Random().Next(10, 100); } // Varsayılan
        }

        // Bu hesaplamalar Prometheus sorguları ile daha doğru yapılır.
        private double CalculateErrorRate() => new Random().NextDouble() * 4.5 + 0.5;
        private double CalculateRequestsPerMinute() => new Random().Next(20, 200);
        private double CalculateAverageResponseTime() => new Random().Next(50, 300);

        // Endpoint metriklerini oluşturma (Genellikle Prometheus'tan sorgulanır)
        private IEnumerable<object> GetEndpointMetrics() => CreateSampleEndpointMetrics();
        private IEnumerable<object> CreateSampleEndpointMetrics()
        {
            return new[]
            {
                CreateEndpointMetric("/api/products", "GET", 120, 80, 250, 120, 2, 118),
                CreateEndpointMetric("/api/products", "POST", 180, 100, 350, 45, 1, 44),
                CreateEndpointMetric("/api/categories", "GET", 90, 60, 180, 85, 0, 85),
                CreateEndpointMetric("/api/brands", "GET", 95, 65, 190, 65, 1, 64),
                CreateEndpointMetric("/api/orders", "GET", 200, 120, 400, 45, 0, 45),
                CreateEndpointMetric("/api/orders", "POST", 250, 150, 500, 35, 2, 33),
                CreateEndpointMetric("/api/users", "GET", 110, 70, 220, 55, 0, 55),
                CreateEndpointMetric("/api/auth/login", "POST", 230, 150, 450, 70, 5, 65),
                CreateEndpointMetric("/api/search", "GET", 180, 100, 360, 90, 1, 89),
                CreateEndpointMetric("/api/payments", "POST", 300, 200, 600, 25, 3, 22),
                CreateEndpointMetric("/api/cart", "GET", 85, 50, 170, 110, 0, 110),
                CreateEndpointMetric("/api/cart", "POST", 95, 60, 190, 65, 1, 64),
                CreateEndpointMetric("/api/checkout", "POST", 350, 250, 700, 30, 4, 26),
                CreateEndpointMetric("/api/notifications", "GET", 75, 45, 150, 40, 0, 40),
                CreateEndpointMetric("/api/images", "GET", 150, 90, 300, 150, 0, 150),
                CreateEndpointMetric("/api/images", "POST", 280, 200, 560, 25, 2, 23),
                CreateEndpointMetric("/api/sitemaps", "GET", 110, 70, 220, 15, 0, 15),
                CreateEndpointMetric("/api/metrics", "GET", 70, 40, 140, 30, 0, 30)
            };
        }
        private object CreateEndpointMetric(string path, string method, double avg, double min, double max, int total, int failed, int success) => new { /*...*/ }; // Önceki kodla aynı

        #endregion
    }
}