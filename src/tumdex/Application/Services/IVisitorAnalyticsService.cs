using Application.Models.Monitoring.Analytics;
using Microsoft.AspNetCore.Http;

namespace Application.Services;

public interface IVisitorAnalyticsService
{
    Task LogVisitAsync(HttpContext context, bool isAuthenticated, string username = null);
    Task<VisitorAnalyticsSummary> GetDailyAnalyticsAsync(DateTime date);
    Task<VisitorAnalyticsSummary> GetDateRangeAnalyticsAsync(DateTime startDate, DateTime endDate);
    Task<List<ReferrerSummary>> GetTopReferrersAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<List<PageViewSummary>> GetTopPagesAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<List<CampaignSummary>> GetTopCampaignsAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<List<GeographySummary>> GetTopLocationsAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<Dictionary<DateTime, int>> GetVisitorTimelineAsync(DateTime startDate, DateTime endDate);
}