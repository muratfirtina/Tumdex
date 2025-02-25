using Application.Abstraction.Services;
using Prometheus;

namespace Infrastructure.Services.Monitoring;

public class PrometheusMetricsService : IMetricsService
{
    // Mevcut metrikler
    #region General Metrics
    private readonly Counter _totalRequests;
    private readonly Counter _errorRequests;
    private readonly Histogram _requestDuration;
    private readonly Counter _securityEvents;
    private readonly Gauge _activeConnections;
    private readonly Counter _rateLimitHits;
    private readonly Counter _ddosAttempts;
    private readonly Counter _cacheHits;
    private readonly Counter _cacheMisses;
    private readonly Counter _alertCounter;
    #endregion
    
    // Yeni eklenen e-ticaret metrikleri
    #region E-Commerce Metrics
    
    private readonly Histogram _paymentProcessingDuration;
    private readonly Counter _paymentTransactions;
    private readonly Counter _failedPayments;
    private readonly Gauge _cartAbandonment;
    private readonly Counter _orderCompletions;
    private readonly Counter _checkoutStarts;
    private readonly Histogram _checkoutDuration;

    #endregion
    
    // API Kullanım metrikleri
    #region API Usage Metrics
    private readonly Counter _apiEndpointCalls;
    private readonly Histogram _apiEndpointLatency;
    private readonly Counter _apiErrors;
    #endregion
    
    // Oturum metrikleri
    #region Session Metrics
    private readonly Gauge _activeUsers;
    private readonly Counter _userLogins;
    private readonly Counter _failedLogins;
    private readonly Histogram _sessionDuration;
    #endregion
    
    // Auth specific metrics
    #region Auth Metrics
    private readonly Counter _failedLoginAttempts;
    private readonly Counter _tokenRefreshes;
    private readonly Counter _authorizationDecisions;
    private readonly Counter _passwordResets;
    private readonly Histogram _authDuration;
    #endregion

    public PrometheusMetricsService()
    {
        // Mevcut metriklerin yapılandırması
        #region General Metrics
        _totalRequests = Metrics.CreateCounter(
            "requests_total",
            "Total number of HTTP requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "endpoint", "status_code" }
            });

        _errorRequests = Metrics.CreateCounter(
            "request_errors_total",
            "Total number of HTTP request errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "endpoint", "error_type" }
            });

        _requestDuration = Metrics.CreateHistogram(
            "request_duration_milliseconds",
            "Duration of HTTP requests in milliseconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method", "endpoint" },
                Buckets = new[] { 0.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0, 5000.0 }
            });
        _activeConnections = Metrics.CreateGauge(
            "active_connections_total",
            "Number of active connections",
            new GaugeConfiguration
            {
                LabelNames = new[] { "type" }
            });

        _rateLimitHits = Metrics.CreateCounter(
            "rate_limit_hits_total",
            "Number of rate limit hits",
            new CounterConfiguration
            {
                LabelNames = new[] { "client_ip", "path", "user_id" }
            });

        _ddosAttempts = Metrics.CreateCounter(
            "ddos_attempts_total",
            "Number of potential DDoS attempts",
            new CounterConfiguration
            {
                LabelNames = new[] { "client_ip", "path" }
            });

        _securityEvents = Metrics.CreateCounter(
            "security_events_total",
            "Total number of security events",
            new CounterConfiguration
            {
                LabelNames = new[] { "event_type", "severity" }
            });
        _alertCounter = Metrics.CreateCounter(
            "application_alerts_total",
            "Total number of alerts triggered",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "severity" }
            });
        
        #endregion

        // Ödeme işlem metrikleri
        #region Payment Metrics
        _paymentProcessingDuration = Metrics.CreateHistogram(
            "payment_processing_duration_seconds",
            "Duration of payment processing",
            new HistogramConfiguration
            {
                LabelNames = new[] { "payment_provider", "payment_type" },
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 }
            });

        _paymentTransactions = Metrics.CreateCounter(
            "payment_transactions_total",
            "Total number of payment transactions",
            new CounterConfiguration
            {
                LabelNames = new[] { "status", "payment_provider", "payment_type" }
            });

        _failedPayments = Metrics.CreateCounter(
            "failed_payments_total",
            "Total number of failed payments",
            new CounterConfiguration
            {
                LabelNames = new[] { "failure_reason", "payment_provider" }
            });
        #endregion

        // Sepet ve sipariş metrikleri
        #region Cart and Order Metrics
        _cartAbandonment = Metrics.CreateGauge(
            "cart_abandonment_rate",
            "Current cart abandonment rate as a percentage",
            new GaugeConfiguration
            {
                LabelNames = new[] { "user_type" }
            });

        _orderCompletions = Metrics.CreateCounter(
            "order_completions_total",
            "Total number of completed orders",
            new CounterConfiguration
            {
                LabelNames = new[] { "order_type", "payment_method" }
            });

        _checkoutStarts = Metrics.CreateCounter(
            "checkout_starts_total",
            "Total number of checkout processes started",
            new CounterConfiguration
            {
                LabelNames = new[] { "user_type" }
            });

        _checkoutDuration = Metrics.CreateHistogram(
            "checkout_duration_seconds",
            "Duration of checkout process",
            new HistogramConfiguration
            {
                LabelNames = new[] { "user_type", "payment_method" },
                Buckets = new[] { 30.0, 60.0, 120.0, 300.0, 600.0 }
            });
        #endregion

        // API Kullanım metrikleri
        #region API Usage Metrics
        _apiEndpointCalls = Metrics.CreateCounter(
            "api_endpoint_calls_total",
            "Total number of API endpoint calls",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "method", "version" }
            });

        _apiEndpointLatency = Metrics.CreateHistogram(
            "api_endpoint_latency_seconds",
            "API endpoint latency in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint", "method" },
                Buckets = new[] { 0.1, 0.3, 0.5, 0.7, 1.0, 2.0, 5.0 }
            });
        #endregion

        // Oturum metrikleri
        #region Session Metrics
        _activeUsers = Metrics.CreateGauge(
            "active_users_total",
            "Number of currently active users",
            new GaugeConfiguration
            {
                LabelNames = new[] { "user_type" }
            });

        _userLogins = Metrics.CreateCounter(
            "user_logins_total",
            "Total number of user logins",
            new CounterConfiguration
            {
                LabelNames = new[] { "auth_method", "user_type" }
            });

        _sessionDuration = Metrics.CreateHistogram(
            "session_duration_minutes",
            "Duration of user sessions",
            new HistogramConfiguration
            {
                LabelNames = new[] { "user_type" },
                Buckets = new[] { 1.0, 5.0, 15.0, 30.0, 60.0, 120.0 }
            });
        #endregion
        
        // Auth metrikleri tanımlamaları
        #region Auth Metrics
        _failedLoginAttempts = Metrics.CreateCounter(
            "failed_login_attempts_total",
            "Total number of failed login attempts",
            new CounterConfiguration
            {
                LabelNames = new[] { "reason", "user_type" }
            });

        _tokenRefreshes = Metrics.CreateCounter(
            "token_refreshes_total",
            "Total number of token refresh operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "token_type", "status" }
            });

        _authorizationDecisions = Metrics.CreateCounter(
            "authorization_decisions_total",
            "Total number of authorization decisions",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "role", "decision" }
            });

        _passwordResets = Metrics.CreateCounter(
            "password_resets_total",
            "Total number of password reset operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "initiation_type", "status" }
            });

        _authDuration = Metrics.CreateHistogram(
            "auth_operation_duration_seconds",
            "Duration of authentication operations",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation_type" },
                Buckets = new[] { 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
            });
        #endregion
        
        _cacheHits = Metrics.CreateCounter(
            "cache_hits_total",
            "Total number of cache hits",
            new CounterConfiguration
            {
                LabelNames = new[] { "cache_key", "cache_type" }
            });

        _cacheMisses = Metrics.CreateCounter(
            "cache_misses_total",
            "Total number of cache misses",
            new CounterConfiguration
            {
                LabelNames = new[] { "cache_key", "cache_type" }
            });
    }
    
    #region Auth Metrics Implementation

    public void IncrementFailedLogins(string reason, string userType = "anonymous")
    {
        _failedLoginAttempts.WithLabels(reason, userType).Inc();
        
        // Güvenlik olayı olarak da kaydet
        RecordSecurityEvent("failed_login", "warning");
    }

    public void RecordTokenRefresh(string tokenType, bool success)
    {
        _tokenRefreshes
            .WithLabels(tokenType, success ? "success" : "failure")
            .Inc();

        if (!success)
        {
            RecordSecurityEvent("token_refresh_failure", "warning");
        }
    }

    public void RecordAuthorizationDecision(string endpoint, string role, bool allowed)
    {
        _authorizationDecisions
            .WithLabels(endpoint, role, allowed ? "allowed" : "denied")
            .Inc();

        if (!allowed)
        {
            RecordSecurityEvent("authorization_denied", "info");
        }
    }

    public void RecordPasswordReset(string initiationType, bool success)
    {
        _passwordResets
            .WithLabels(initiationType, success ? "success" : "failure")
            .Inc();

        var severity = success ? "info" : "warning";
        RecordSecurityEvent("password_reset", severity);
    }

    public void RecordAuthOperationDuration(string operationType, double duration)
    {
        _authDuration.WithLabels(operationType).Observe(duration);
        
        // Eğer operasyon çok uzun sürüyorsa uyarı oluştur
        if (duration > 5.0) // 5 saniyeden uzun
        {
            RecordSecurityEvent("slow_auth_operation", "warning");
        }
    }

    #endregion

    // Ödeme metrik metodları
    #region Payment Metrics Implementation
    public void RecordPaymentDuration(string provider, string type, double duration)
    {
        _paymentProcessingDuration.WithLabels(provider, type).Observe(duration);
    }

    public void IncrementPaymentTransactions(string status, string provider, string type)
    {
        _paymentTransactions.WithLabels(status, provider, type).Inc();
    }

    public void IncrementFailedPayments(string reason, string provider)
    {
        _failedPayments.WithLabels(reason, provider).Inc();
    }
    #endregion

    // Sepet metrik metodları
    #region Cart and Order Metrics Implementation
    public void UpdateCartAbandonment(string userType, double rate)
    {
        _cartAbandonment.WithLabels(userType).Set(rate);
    }

    public void IncrementOrderCompletion(string orderType, string paymentMethod)
    {
        _orderCompletions.WithLabels(orderType, paymentMethod).Inc();
    }

    public void RecordCheckoutDuration(string userType, string paymentMethod, double duration)
    {
        _checkoutDuration.WithLabels(userType, paymentMethod).Observe(duration);
    }
    #endregion

    // API Kullanım metrik metodları
    #region API Usage Metrics Implementation
    public void RecordApiCall(string endpoint, string method, string version)
    {
        _apiEndpointCalls.WithLabels(endpoint, method, version).Inc();
    }

    public void RecordApiLatency(string endpoint, string method, double duration)
    {
        _apiEndpointLatency.WithLabels(endpoint, method).Observe(duration);
    }
    #endregion

    // Oturum metrik metodları
    #region Session Metrics Implementation
    public void UpdateActiveUsers(string userType, int count)
    {
        _activeUsers.WithLabels(userType).Set(count);
    }

    public void IncrementUserLogins(string authMethod, string userType)
    {
        _userLogins.WithLabels(authMethod, userType).Inc();
    }

    public void RecordSessionDuration(string userType, double duration)
    {
        _sessionDuration.WithLabels(userType).Observe(duration);
    }
    #endregion
    
    

    #region General Metrics Implementation
    public void IncrementRateLimitHit(string clientIp, string path, string userId = "anonymous")
    {
        _rateLimitHits.WithLabels(clientIp, path, userId).Inc();
    }

    public void IncrementDdosAttempt(string clientIp, string path)
    {
        _ddosAttempts.WithLabels(clientIp, path).Inc();
    }

    public void RecordRequestDuration(string method, string endpoint, double durationMs)
    {
        _requestDuration.WithLabels(method, endpoint).Observe(durationMs);
    }

    public void IncrementCacheHit(string cacheKey, string cacheType = "redis")
    {
        try
        {
            _cacheHits?.WithLabels(cacheKey, cacheType).Inc();
        }
        catch (Exception ex)
        {
            // Metrik hatalarını logla ama uygulamayı kesme
            Console.WriteLine($"Error recording cache hit metric: {ex.Message}");
        }
    }

    public void IncrementCacheMiss(string cacheKey, string cacheType = "redis")
    {
        try
        {
            _cacheMisses?.WithLabels(cacheKey, cacheType).Inc();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording cache miss metric: {ex.Message}");
        }
    }

    public void IncrementAlertCounter(string alertType, Dictionary<string, string> labels)
    {
        try
        {
            if (string.IsNullOrEmpty(alertType))
                alertType = "unknown";

            var severity = labels?.GetValueOrDefault("severity", "info") ?? "info";
            _alertCounter.WithLabels(alertType, severity).Inc();
        }
        catch (Exception ex)
        {
            // Log the error but don't throw
            // Metrik kaydı başarısız olsa bile uygulamanın çalışmaya devam etmesi önemli
            Console.WriteLine($"Error incrementing alert counter: {ex.Message}");
        }
    }

    public void IncrementTotalRequests(string method, string endpoint, string statusCode)
    {
        _totalRequests.WithLabels(method, endpoint, statusCode).Inc();
    }

    public void TrackActiveConnection(string type, int delta)
    {
        if (string.IsNullOrEmpty(type))
            type = "unknown";

        if (delta > 0)
            _activeConnections.WithLabels(type).Inc(delta);
        else
            _activeConnections.WithLabels(type).Dec(Math.Abs(delta));
    }

    public void RecordSecurityEvent(string eventType, string severity)
    {
        _securityEvents.WithLabels(eventType, severity).Inc();
    }
    #endregion
}