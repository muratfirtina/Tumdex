using Application.Repositories;
using Core.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Context;
using Application.Models.Monitoring.Analytics;
using Domain.Entities;

namespace Persistence.Repositories;

public class VisitorAnalyticsRepository : EfRepositoryBase<VisitorTrackingEvent, string, TumdexDbContext>, IVisitorAnalyticsRepository
{
    private readonly ILogger<VisitorAnalyticsRepository> _logger;

    public VisitorAnalyticsRepository(TumdexDbContext context, ILogger<VisitorAnalyticsRepository> logger) 
        : base(context)
    {
        _logger = logger;
    }

    public async Task LogVisitAsync(VisitorTrackingEvent visit)
    {
        try
        {
            // Repository Pattern'deki AddAsync metodunu kullan
            await AddAsync(visit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ziyaret kayıt edilirken hata: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<VisitorAnalyticsSummary> GetDailyAnalyticsAsync(DateTime date)
    {
        try
        {
            // Günün başlangıcı ve bitişi
            var startDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddDays(1);

            // Tüm ziyaretleri getir
            var visits = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= startDate && v.VisitTime < endDate)
                .ToListAsync();

            return GenerateAnalyticsSummary(visits, startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Günlük analiz hesaplanırken hata: {Date}", date);
            throw;
        }
    }

    public async Task<VisitorAnalyticsSummary> GetDateRangeAnalyticsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // UTC için standardize et
            var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

            // Tüm ziyaretleri getir
            var visits = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate)
                .ToListAsync();

            var summary = GenerateAnalyticsSummary(visits, utcStartDate, utcEndDate);

            // Günlük kırılımı ekle
            var dailyGroups = visits
                .GroupBy(v => v.VisitTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Date, x => x.Count);

            summary.DailyBreakdown = dailyGroups;

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tarih aralığı analizi hesaplanırken hata: {StartDate} - {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    public async Task<List<ReferrerSummary>> GetTopReferrersAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        try
        {
            var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

            // Boş referrer'ları hariç tut
            var referrers = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate && !string.IsNullOrEmpty(v.ReferrerDomain))
                .GroupBy(v => new { v.ReferrerDomain, v.ReferrerType })
                .Select(g => new ReferrerSummary
                {
                    Domain = g.Key.ReferrerDomain,
                    Type = g.Key.ReferrerType,
                    VisitCount = g.Count(),
                    UniqueVisitorCount = g.Select(v => v.SessionId).Distinct().Count(),
                    // Bounce rate ve average visit duration hesaplaması için daha karmaşık logic gerekebilir
                    BounceRate = 0,
                    AverageVisitDuration = TimeSpan.Zero
                })
                .OrderByDescending(r => r.VisitCount)
                .Take(limit)
                .ToListAsync();

            // Doğrudan girişler için ayrı bir kayıt ekleyelim
            var directVisitsCount = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate && 
                       (string.IsNullOrEmpty(v.ReferrerDomain) || v.ReferrerType == "Doğrudan"))
                .CountAsync();

            if (directVisitsCount > 0)
            {
                var uniqueDirectVisitorsCount = await Context.Set<VisitorTrackingEvent>()
                    .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate && 
                           (string.IsNullOrEmpty(v.ReferrerDomain) || v.ReferrerType == "Doğrudan"))
                    .Select(v => v.SessionId)
                    .Distinct()
                    .CountAsync();

                referrers.Add(new ReferrerSummary
                {
                    Domain = "Doğrudan Giriş",
                    Type = "Doğrudan",
                    VisitCount = directVisitsCount,
                    UniqueVisitorCount = uniqueDirectVisitorsCount,
                    BounceRate = 0,
                    AverageVisitDuration = TimeSpan.Zero
                });
            }

            // Yeniden sırala
            return referrers.OrderByDescending(r => r.VisitCount).Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Top referrerlar hesaplanırken hata: {StartDate} - {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    public async Task<List<PageViewSummary>> GetTopPagesAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        try
        {
            var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

            // En çok ziyaret edilen sayfaları alma
            var pages = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate)
                .GroupBy(v => v.Page)
                .Select(g => new PageViewSummary
                {
                    PageUrl = g.Key,
                    PageTitle = "", // Title bilgisi için ayrı bir mekanizma gerekli
                    ViewCount = g.Count(),
                    UniqueViewerCount = g.Select(v => v.SessionId).Distinct().Count(),
                    // Sayfada kalma süresi hesaplaması daha karmaşık
                    AverageTimeOnPage = 0,
                    ExitRate = 0
                })
                .OrderByDescending(p => p.ViewCount)
                .Take(limit)
                .ToListAsync();

            return pages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Top sayfalar hesaplanırken hata: {StartDate} - {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    public async Task<List<CampaignSummary>> GetTopCampaignsAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        try
        {
            var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

            // Filtrele: UTM parametreli ziyaretler
            var campaigns = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate && 
                       !string.IsNullOrEmpty(v.UTMSource))
                .GroupBy(v => new { v.UTMSource, v.UTMMedium, v.UTMCampaign })
                .Select(g => new CampaignSummary
                {
                    Source = g.Key.UTMSource ?? "Belirtilmemiş",
                    Medium = g.Key.UTMMedium ?? "Belirtilmemiş",
                    Campaign = g.Key.UTMCampaign ?? "Belirtilmemiş",
                    VisitCount = g.Count(),
                    // Dönüşüm oranı hesaplaması için daha fazla veri gerekir
                    ConversionCount = 0,
                    ConversionRate = 0
                })
                .OrderByDescending(c => c.VisitCount)
                .Take(limit)
                .ToListAsync();

            return campaigns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UTM kampanyaları hesaplanırken hata: {StartDate} - {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    public async Task<List<GeographySummary>> GetTopLocationsAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        try
        {
            var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

            // Filtrele: Konum bilgisi olan ziyaretler
            var locations = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate && 
                       !string.IsNullOrEmpty(v.Country))
                .GroupBy(v => new { v.Country, v.City })
                .Select(g => new GeographySummary
                {
                    Country = g.Key.Country,
                    City = g.Key.City ?? "Belirtilmemiş",
                    VisitCount = g.Count()
                })
                .OrderByDescending(l => l.VisitCount)
                .Take(limit)
                .ToListAsync();

            // Eğer çok az veya hiç konum yoksa, varsayılan olarak "Bilinmiyor" ekle
            if (locations.Count == 0)
            {
                var unknownLocationCount = await Context.Set<VisitorTrackingEvent>()
                    .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate)
                    .CountAsync();

                if (unknownLocationCount > 0)
                {
                    locations.Add(new GeographySummary
                    {
                        Country = "Bilinmiyor",
                        City = "Bilinmiyor",
                        VisitCount = unknownLocationCount
                    });
                }
            }

            return locations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coğrafi konum analizi hesaplanırken hata: {StartDate} - {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    public async Task<Dictionary<DateTime, int>> GetVisitorTimelineAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

            // Günlük ziyaretçi sayısını al
            var timeline = await Context.Set<VisitorTrackingEvent>()
                .Where(v => v.VisitTime >= utcStartDate && v.VisitTime <= utcEndDate)
                .GroupBy(v => v.VisitTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Date, x => x.Count);

            // Eksik günleri doldur
            var currentDate = utcStartDate.Date;
            while (currentDate <= utcEndDate.Date)
            {
                if (!timeline.ContainsKey(currentDate))
                {
                    timeline[currentDate] = 0;
                }
                currentDate = currentDate.AddDays(1);
            }

            return timeline;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ziyaretçi zaman çizelgesi hesaplanırken hata: {StartDate} - {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    #region Helper Methods

    private VisitorAnalyticsSummary GenerateAnalyticsSummary(
        List<VisitorTrackingEvent> visits, 
        DateTime startDate, 
        DateTime endDate)
    {
        var summary = new VisitorAnalyticsSummary
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalVisits = visits.Count,
            UniqueVisitors = visits.Select(v => v.SessionId).Distinct().Count(),
            AuthenticatedUsers = visits.Count(v => v.IsAuthenticated),
            AnonymousUsers = visits.Count(v => !v.IsAuthenticated),
            NewVisitors = visits.Count(v => v.IsNewVisitor),
            ReturningVisitors = visits.Count(v => !v.IsNewVisitor)
        };

        // Cihaz dağılımı
        summary.DeviceBreakdown = visits
            .GroupBy(v => string.IsNullOrEmpty(v.DeviceType) ? "Bilinmiyor" : v.DeviceType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Tarayıcı dağılımı
        summary.BrowserBreakdown = visits
            .GroupBy(v => string.IsNullOrEmpty(v.BrowserName) ? "Bilinmiyor" : v.BrowserName)
            .ToDictionary(g => g.Key, g => g.Count());

        // Trafik kaynakları
        summary.ReferrerTypeBreakdown = visits
            .GroupBy(v => string.IsNullOrEmpty(v.ReferrerType) ? "Doğrudan" : v.ReferrerType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Saatlik kırılım
        summary.HourlyBreakdown = visits
            .GroupBy(v => v.VisitTime.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        return summary;
    }

    #endregion
}