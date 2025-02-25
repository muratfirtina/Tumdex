using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Features.Carts.Commands.AddItemToCart;
using Application.Features.Orders.Commands.Create;
using Application.Features.Users.Commands.LoginUser;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

public class MetricsBehavior<TRequest, TResponse> : BaseBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MetricsBehavior<TRequest, TResponse>> _logger;

    public MetricsBehavior(
        ILogger<MetricsBehavior<TRequest, TResponse>> logger,
        IMetricsService metricsService) 
        : base(logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    public override async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = GetRequestName(request);
        var operationType = DetermineOperationType(requestName);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // API çağrı metriği
            _metricsService.RecordApiCall(
                endpoint: requestName,
                method: operationType,
                version: "v1");

            var response = await base.Handle(request, next, cancellationToken);
            stopwatch.Stop();

            // Spesifik işlem metrikleri
            await HandleSpecificMetrics(request, stopwatch.Elapsed.TotalSeconds);

            // Genel istek süresi metriği
            _metricsService.RecordRequestDuration(
                operationType,
                requestName,
                stopwatch.Elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _metricsService.IncrementTotalRequests(
                operationType,
                requestName,
                "error");

            // Hata durumuna göre özel metrikler
            HandleErrorMetrics(request, ex);
            throw;
        }
    }

    private async Task HandleSpecificMetrics(TRequest request, double duration)
    {
        // Sipariş işlemleri metrikleri
        if (request is ConvertCartToOrderCommand)
        {
            _metricsService.RecordCheckoutDuration("registered", "standard", duration);
            _metricsService.IncrementOrderCompletion("standard", "not_specified");
            _logger.LogInformation("New order completion recorded. Duration: {Duration}s", duration);
        }

        // Sepet işlemleri metrikleri
        else if (request is CreateCartCommand)
        {
            _metricsService.UpdateCartAbandonment("active", 0); // Sepete ürün eklendiğinde terk oranı sıfırlanır
            _logger.LogInformation("Cart creation recorded");
        }

        // Kullanıcı işlemleri metrikleri
        else if (request is LoginUserRequest)
        {
            _metricsService.IncrementUserLogins("jwt", "standard");
            _metricsService.UpdateActiveUsers("authenticated", 1);
            _logger.LogInformation("User login recorded");
        }

        // API Endpoint performans metrikleri
        if (duration > 1.0) // 1 saniyeden uzun süren istekler
        {
            _metricsService.RecordApiLatency(
                GetRequestName(request),
                DetermineOperationType(GetRequestName(request)),
                duration);
            
            _logger.LogWarning("Long running request detected: {RequestName}, Duration: {Duration}s", 
                GetRequestName(request), duration);
        }
    }

    private void HandleErrorMetrics(TRequest request, Exception ex)
    {
        // Sipariş hataları
        if (request is ConvertCartToOrderCommand)
        {
            _metricsService.IncrementFailedPayments(
                ex.GetType().Name,
                "order_creation");
        }
        
        // Kullanıcı işlem hataları
        else if (request is LoginUserRequest)
        {
            _metricsService.RecordSecurityEvent(
                "failed_login",
                "warning");
        }

        _logger.LogError(ex, "Error processing request: {RequestName}", GetRequestName(request));
    }

    private string DetermineOperationType(string requestName)
    {
        if (requestName.EndsWith("Command"))
            return "Command";
        if (requestName.EndsWith("Query"))
            return "Query";
        return "Unknown";
    }

    private string GetMetricCategory(TRequest request)
    {
        if (request.GetType().Namespace?.Contains("Orders") == true)
            return "order";
        if (request.GetType().Namespace?.Contains("Carts") == true)
            return "cart";
        if (request.GetType().Namespace?.Contains("Users") == true)
            return "user";
        return "other";
    }
}