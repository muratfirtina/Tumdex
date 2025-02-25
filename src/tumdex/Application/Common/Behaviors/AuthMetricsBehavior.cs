using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.LogoutUser;
using Application.Features.Users.Commands.PasswordReset;
using Application.Features.Users.Commands.RefreshTokenLogin;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

public class AuthMetricsBehavior<TRequest, TResponse> : BaseBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<AuthMetricsBehavior<TRequest, TResponse>> _logger;

    public AuthMetricsBehavior(
        ILogger<AuthMetricsBehavior<TRequest, TResponse>> logger,
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
        var stopwatch = Stopwatch.StartNew();
        var requestName = GetRequestName(request);

        try
        {
            // API çağrısını kaydet
            _metricsService.RecordApiCall(
                endpoint: $"auth/{requestName}",
                method: "POST",
                version: "v1");

            var response = await base.Handle(request, next, cancellationToken);
            stopwatch.Stop();

            // İşlem tipine göre spesifik metrikler
            await HandleAuthMetrics(request, stopwatch.Elapsed.TotalSeconds);

            return response;
        }
        catch (Exception ex)
        {
            HandleAuthError(request, ex);
            throw;
        }
    }

    private async Task HandleAuthMetrics(TRequest request, double duration)
    {
        switch (request)
        {
            case LoginUserRequest:
                _metricsService.IncrementUserLogins("jwt", "standard");
                _metricsService.UpdateActiveUsers("authenticated", 1);
                break;

            case LogoutUserCommand:
                _metricsService.UpdateActiveUsers("authenticated", -1);
                break;

            case PasswordResetRequest:
                _metricsService.RecordSecurityEvent(
                    "password_reset_request",
                    "info");
                break;

            case RefreshTokenLoginRequest:
                _metricsService.RecordSecurityEvent(
                    "token_refresh",
                    "info");
                break;
        }

        // Yetkilendirme işlem süresi metriği
        if (request.GetType().Namespace?.Contains("Authorization") == true)
        {
            _metricsService.RecordRequestDuration(
                "authorization",
                GetRequestName(request),
                duration * 1000); // milliseconds
        }

        // API Endpoint performans metriği
        if (duration > 1.0) // 1 saniyeden uzun süren istekler
        {
            _metricsService.RecordApiLatency(
                GetRequestName(request),
                "auth",
                duration);

            _logger.LogWarning("Long running auth request detected: {RequestName}, Duration: {Duration}s",
                GetRequestName(request), duration);
        }
    }

    private void HandleAuthError(TRequest request, Exception ex)
    {
        var severity = DetermineErrorSeverity(request, ex);
        
        _metricsService.RecordSecurityEvent(
            eventType: GetSecurityEventType(request),
            severity: severity);

        _logger.LogError(ex, "Auth error in {RequestName}: {ErrorMessage}",
            GetRequestName(request), ex.Message);
    }

    private string GetSecurityEventType(TRequest request)
    {
        return request switch
        {
            LoginUserRequest => "failed_login",
            PasswordResetRequest => "failed_password_reset",
            RefreshTokenLoginRequest => "failed_token_refresh",
            _ => "auth_error"
        };
    }

    private string DetermineErrorSeverity(TRequest request, Exception ex)
    {
        // Login denemeleri için özel kontrol
        if (request is LoginUserRequest && ex.Message.Contains("Invalid credentials"))
            return "warning";

        // Token ile ilgili hatalar
        if (ex.Message.Contains("token") || ex.Message.Contains("JWT"))
            return "warning";

        // Diğer durumlar için
        return "error";
    }
}