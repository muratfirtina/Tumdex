using Application.Models.Monitoring.Analytics;
using Core.Persistence.Repositories;
using Domain.Entities;

namespace Application.Repositories;

public interface IVisitorAnalyticsRepository : IAsyncRepository<VisitorTrackingEvent, string>, IRepository<VisitorTrackingEvent, string>
{
    Task LogVisitAsync(VisitorTrackingEvent visit);
    Task<VisitorAnalyticsSummary> GetDailyAnalyticsAsync(DateTime date);
    Task<VisitorAnalyticsSummary> GetDateRangeAnalyticsAsync(DateTime startDate, DateTime endDate);
    Task<List<ReferrerSummary>> GetTopReferrersAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<List<PageViewSummary>> GetTopPagesAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<List<CampaignSummary>> GetTopCampaignsAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<List<GeographySummary>> GetTopLocationsAsync(DateTime startDate, DateTime endDate, int limit = 10);
    Task<Dictionary<DateTime, int>> GetVisitorTimelineAsync(DateTime startDate, DateTime endDate);
}