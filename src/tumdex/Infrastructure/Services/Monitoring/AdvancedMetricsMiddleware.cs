using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Prometheus;

namespace Infrastructure.Services.Monitoring;

public class AdvancedMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Counter _totalRequests;
    private readonly Counter _errorRequests;
    private readonly Histogram _requestDuration;
    private readonly Counter _securityEvents;
    private readonly Gauge _activeConnections;
    private readonly Counter _databaseErrors;

    public AdvancedMetricsMiddleware(RequestDelegate next)
    {
        _next = next;

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

        _securityEvents = Metrics.CreateCounter(
            "security_events_total",
            "Total number of security events",
            new CounterConfiguration
            {
                LabelNames = new[] { "event_type", "severity" }
            });

        _activeConnections = Metrics.CreateGauge(
            "active_connections",
            "Number of currently active connections",
            new GaugeConfiguration
            {
                LabelNames = new[] { "type" }
            });

        _databaseErrors = Metrics.CreateCounter(
            "database_connection_errors_total",
            "Total number of database connection errors"
        );
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        _activeConnections.WithLabels("http").Inc();

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);

            sw.Stop();
            _requestDuration
                .WithLabels(method, path)
                .Observe(sw.ElapsedMilliseconds);

            _totalRequests
                .WithLabels(method, path, context.Response.StatusCode.ToString())
                .Inc();

            if (context.Response.StatusCode >= 400)
            {
                _errorRequests
                    .WithLabels(method, path, "http_" + context.Response.StatusCode)
                    .Inc();
            }
        }
        catch (Exception ex)
        {
            _errorRequests
                .WithLabels(method, path, ex.GetType().Name)
                .Inc();

            if (ex is DbException)
            {
                _databaseErrors.Inc();
            }

            throw;
        }
        finally
        {
            _activeConnections.WithLabels("http").Dec();
        }
    }
}