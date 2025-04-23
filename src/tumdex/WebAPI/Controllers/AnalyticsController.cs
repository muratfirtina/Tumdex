using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Models.Monitoring.Analytics;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Admin")]
    [ApiController]
    public class AnalyticsController : BaseController
    {
        private readonly IVisitorAnalyticsService _analyticsService;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IVisitorAnalyticsService analyticsService,
            IGoogleAnalyticsService googleAnalyticsService,
            ILogger<AnalyticsController> logger)
        {
            _analyticsService = analyticsService;
            _googleAnalyticsService = googleAnalyticsService;
            _logger = logger;
        }

        // Özet istatistikler
        [HttpGet("summary")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Summary Analytics", Menu = AuthorizeDefinitionConstants.Analytics)]
        public async Task<IActionResult> GetSummary(DateTime startDate, DateTime endDate, string source = "internal")
        {
            try
            {
                // UTC'ye çevirme
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                // Veri kaynağına göre işlem yapma
                if (source.ToLower() == "ga")
                {
                    // Google Analytics'ten veri al
                    // Burada GA verilerini doğrudan istemek yerine, çeşitli GA çağrılarını birleştirip özet oluşturacağız
                    var visitorTimeline = await _googleAnalyticsService.GetVisitorsTimelineAsync(utcStartDate, utcEndDate);
                    var deviceStats = await _googleAnalyticsService.GetDeviceBreakdownAsync(utcStartDate, utcEndDate);
                    
                    // Verileri birleştirerek özet oluştur
                    var summary = new VisitorAnalyticsSummary
                    {
                        StartDate = utcStartDate,
                        EndDate = utcEndDate,
                        TotalVisits = visitorTimeline.Sum(v => v.Sessions),
                        UniqueVisitors = visitorTimeline.Sum(v => v.TotalVisitors),
                        NewVisitors = visitorTimeline.Sum(v => v.NewVisitors),
                        ReturningVisitors = visitorTimeline.Sum(v => v.TotalVisitors - v.NewVisitors),
                        
                        // Cihaz dağılımı
                        DeviceBreakdown = new Dictionary<string, int>
                        {
                            { "Masaüstü", deviceStats.Desktop },
                            { "Mobil", deviceStats.Mobile },
                            { "Tablet", deviceStats.Tablet }
                        },
                        
                        // Tarayıcı dağılımı
                        BrowserBreakdown = deviceStats.Browsers,
                        
                        // Günlük kırılım
                        DailyBreakdown = visitorTimeline.ToDictionary(v => v.Date, v => v.TotalVisitors)
                    };
                    
                    return Ok(summary);
                }
                else if (source.ToLower() == "combined")
                {
                    // Hem kendi verilerimiz hem de GA verilerini birleştir
                    var internalSummary = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                    var gaTimeline = await _googleAnalyticsService.GetVisitorsTimelineAsync(utcStartDate, utcEndDate);
                    var gaDeviceStats = await _googleAnalyticsService.GetDeviceBreakdownAsync(utcStartDate, utcEndDate);
                    
                    // GA verilerini ekleme (örnek olarak sadece toplam ziyaret)
                    internalSummary.TotalVisits += gaTimeline.Sum(v => v.Sessions);
                    internalSummary.UniqueVisitors += gaTimeline.Sum(v => v.TotalVisitors);
                    
                    return Ok(internalSummary);
                }
                else
                {
                    // Kendi verilerimizi getir
                    var summary = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                    return Ok(summary);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Özet veri alınırken hata oluştu");
                return StatusCode(500, new { message = "İstatistik verileri alınırken bir hata oluştu." });
            }
        }

        // En popüler yönlendiriciler
        [HttpGet("top-referrers")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Top Referrers", Menu = AuthorizeDefinitionConstants.Analytics)]
        public async Task<IActionResult> GetTopReferrers(DateTime startDate, DateTime endDate, string source = "internal", int limit = 20)
        {
            try
            {
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                if (source.ToLower() == "ga")
                {
                    // Google Analytics'ten referrerlar
                    var referrers = await _googleAnalyticsService.GetTopReferrersAsync(utcStartDate, utcEndDate, limit);
                    
                    // Veri dönüşümü için ReferrerSummary listesine çevir
                    var result = referrers.Select(r => new ReferrerSummary
                    {
                        Domain = r.Key,
                        Type = DetermineReferrerType(r.Key),
                        VisitCount = r.Value,
                        UniqueVisitorCount = r.Value // GA tarafında ayrıntılı kırılım almak karmaşık, yaklaşık değer kullanıyoruz
                    }).ToList();
                    
                    return Ok(result);
                }
                else if (source.ToLower() == "combined")
                {
                    // Hem kendi veritabanımız hem de GA'den referrerlar
                    var internalReferrers = await _analyticsService.GetTopReferrersAsync(utcStartDate, utcEndDate, limit);
                    var gaReferrers = await _googleAnalyticsService.GetTopReferrersAsync(utcStartDate, utcEndDate, limit);
                    
                    // Verileri birleştir
                    var combinedReferrers = new Dictionary<string, ReferrerSummary>();
                    
                    // Önce iç verileri ekle
                    foreach (var ref1 in internalReferrers)
                    {
                        combinedReferrers[ref1.Domain] = ref1;
                    }
                    
                    // GA verilerini ekle/güncelle
                    foreach (var ref2 in gaReferrers)
                    {
                        if (combinedReferrers.TryGetValue(ref2.Key, out var existingRef))
                        {
                            // Varsa güncelle
                            existingRef.VisitCount += ref2.Value;
                            existingRef.UniqueVisitorCount += ref2.Value; // Yaklaşık değer
                        }
                        else
                        {
                            // Yoksa ekle
                            combinedReferrers[ref2.Key] = new ReferrerSummary
                            {
                                Domain = ref2.Key,
                                Type = DetermineReferrerType(ref2.Key),
                                VisitCount = ref2.Value,
                                UniqueVisitorCount = ref2.Value // Yaklaşık değer
                            };
                        }
                    }
                    
                    // Birleşik sonuçları sıralama ve sınırlama
                    var result = combinedReferrers.Values
                        .OrderByDescending(r => r.VisitCount)
                        .Take(limit)
                        .ToList();
                    
                    return Ok(result);
                }
                else
                {
                    // Kendi veritabanımızdan referrerlar
                    var referrers = await _analyticsService.GetTopReferrersAsync(utcStartDate, utcEndDate, limit);
                    return Ok(referrers);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Top referrerlar alınırken hata oluştu");
                return StatusCode(500, new { message = "Yönlendirici verileri alınırken bir hata oluştu." });
            }
        }

        // En popüler sayfalar
        [HttpGet("top-pages")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Top Pages", Menu = AuthorizeDefinitionConstants.Analytics)]
        public async Task<IActionResult> GetTopPages(DateTime startDate, DateTime endDate, string source = "internal", int limit = 20)
        {
            try
            {
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                if (source.ToLower() == "ga")
                {
                    // Google Analytics'ten popüler sayfalar
                    var pages = await _googleAnalyticsService.GetTopPagesAsync(utcStartDate, utcEndDate, limit);
                    
                    // PageViewSummary formatına dönüştür
                    var result = pages.Select(p => new PageViewSummary
                    {
                        PageUrl = p.Key,
                        PageTitle = "", // GA'den başlık almak daha karmaşık
                        ViewCount = p.Value,
                        UniqueViewerCount = p.Value * 8 / 10 // Yaklaşık değer (%80 benzersiz)
                    }).ToList();
                    
                    return Ok(result);
                }
                else if (source.ToLower() == "combined")
                {
                    // Hem kendi veritabanımız hem de GA'den sayfalar
                    var internalPages = await _analyticsService.GetTopPagesAsync(utcStartDate, utcEndDate, limit);
                    var gaPages = await _googleAnalyticsService.GetTopPagesAsync(utcStartDate, utcEndDate, limit);
                    
                    // Verileri birleştir
                    var combinedPages = new Dictionary<string, PageViewSummary>();
                    
                    // Önce iç verileri ekle
                    foreach (var page1 in internalPages)
                    {
                        combinedPages[page1.PageUrl] = page1;
                    }
                    
                    // GA verilerini ekle/güncelle
                    foreach (var page2 in gaPages)
                    {
                        if (combinedPages.TryGetValue(page2.Key, out var existingPage))
                        {
                            // Varsa güncelle
                            existingPage.ViewCount += page2.Value;
                            existingPage.UniqueViewerCount += page2.Value * 8 / 10; // Yaklaşık değer
                        }
                        else
                        {
                            // Yoksa ekle
                            combinedPages[page2.Key] = new PageViewSummary
                            {
                                PageUrl = page2.Key,
                                PageTitle = "", // Başlık bilgisi yok
                                ViewCount = page2.Value,
                                UniqueViewerCount = page2.Value * 8 / 10 // Yaklaşık değer
                            };
                        }
                    }
                    
                    // Birleşik sonuçları sıralama ve sınırlama
                    var result = combinedPages.Values
                        .OrderByDescending(p => p.ViewCount)
                        .Take(limit)
                        .ToList();
                    
                    return Ok(result);
                }
                else
                {
                    // Kendi veritabanımızdan sayfalar
                    var pages = await _analyticsService.GetTopPagesAsync(utcStartDate, utcEndDate, limit);
                    return Ok(pages);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Top sayfalar alınırken hata oluştu");
                return StatusCode(500, new { message = "Sayfa görüntüleme verileri alınırken bir hata oluştu." });
            }
        }

        // Cihaz istatistikleri
        [HttpGet("device-stats")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Device Stats", Menu = AuthorizeDefinitionConstants.Analytics)]
        public async Task<IActionResult> GetDeviceStats(DateTime startDate, DateTime endDate, string source = "internal")
        {
            try
            {
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                if (source.ToLower() == "ga")
                {
                    // Google Analytics'ten cihaz verileri
                    var deviceStats = await _googleAnalyticsService.GetDeviceBreakdownAsync(utcStartDate, utcEndDate);
                    return Ok(deviceStats);
                }
                else if (source.ToLower() == "combined")
                {
                    // İç veriler ve GA verilerini birleştir
                    var summary = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                    var gaDeviceStats = await _googleAnalyticsService.GetDeviceBreakdownAsync(utcStartDate, utcEndDate);
                    
                    // DeviceStats nesnesi oluştur
                    var result = new
                    {
                        desktop = (summary.DeviceBreakdown.TryGetValue("Masaüstü", out var desktop) ? desktop : 0) + gaDeviceStats.Desktop,
                        mobile = (summary.DeviceBreakdown.TryGetValue("Mobil", out var mobile) ? mobile : 0) + gaDeviceStats.Mobile,
                        tablet = (summary.DeviceBreakdown.TryGetValue("Tablet", out var tablet) ? tablet : 0) + gaDeviceStats.Tablet,
                        browsers = MergeBrowserStats(summary.BrowserBreakdown, gaDeviceStats.Browsers)
                    };
                    
                    return Ok(result);
                }
                else
                {
                    // Kendi verilerimizden cihaz istatistikleri
                    var summary = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                    
                    var result = new
                    {
                        desktop = summary.DeviceBreakdown.TryGetValue("Masaüstü", out var desktop) ? desktop : 0,
                        mobile = summary.DeviceBreakdown.TryGetValue("Mobil", out var mobile) ? mobile : 0,
                        tablet = summary.DeviceBreakdown.TryGetValue("Tablet", out var tablet) ? tablet : 0,
                        browsers = summary.BrowserBreakdown
                    };
                    
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cihaz istatistikleri alınırken hata oluştu");
                return StatusCode(500, new { message = "Cihaz verileri alınırken bir hata oluştu." });
            }
        }

        // Ziyaretçi zaman çizelgesi
        [HttpGet("visitor-timeline")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Visitor Timeline", Menu = AuthorizeDefinitionConstants.Analytics)]
        public async Task<IActionResult> GetVisitorTimeline(DateTime startDate, DateTime endDate, string source = "internal")
        {
            try
            {
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                if (source.ToLower() == "ga")
                {
                    // Google Analytics'ten zaman çizelgesi
                    var timeline = await _googleAnalyticsService.GetVisitorsTimelineAsync(utcStartDate, utcEndDate);
                    return Ok(timeline);
                }
                else if (source.ToLower() == "combined")
                {
                    // İç veriler ve GA verilerini birleştir
                    var summary = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                    var gaTimeline = await _googleAnalyticsService.GetVisitorsTimelineAsync(utcStartDate, utcEndDate);
                    
                    // Birleştirilmiş zaman çizelgesi
                    var combinedTimeline = new List<object>();
                    
                    // Tüm tarihleri içeren bir hash set
                    var allDates = new HashSet<DateTime>();
                    foreach (var date in summary.DailyBreakdown.Keys)
                    {
                        allDates.Add(date.Date);
                    }
                    
                    foreach (var item in gaTimeline)
                    {
                        allDates.Add(item.Date.Date);
                    }
                    
                    // Her tarih için birleştirilmiş veri oluştur
                    foreach (var date in allDates.OrderBy(d => d))
                    {
                        // TUMDEX verileri
                        summary.DailyBreakdown.TryGetValue(date, out var internalVisits);
                        
                        // GA verileri
                        var gaItem = gaTimeline.FirstOrDefault(t => t.Date.Date == date.Date);
                        
                        combinedTimeline.Add(new
                        {
                            date = date.ToString("yyyy-MM-dd"),
                            internalVisits,
                            gaVisits = gaItem?.TotalVisitors ?? 0,
                            totalVisits = internalVisits + (gaItem?.TotalVisitors ?? 0)
                        });
                    }
                    
                    return Ok(combinedTimeline);
                }
                else
                {
                    // Kendi verilerimizden zaman çizelgesi
                    var summary = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                    
                    var timeline = summary.DailyBreakdown
                        .OrderBy(d => d.Key)
                        .Select(d => new
                        {
                            date = d.Key.ToString("yyyy-MM-dd"),
                            visits = d.Value
                        })
                        .ToList();
                    
                    return Ok(timeline);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Zaman çizelgesi alınırken hata oluştu");
                return StatusCode(500, new { message = "Zaman çizelgesi verileri alınırken bir hata oluştu." });
            }
        }

        // Google Analytics entegrasyon durumu
        [HttpGet("ga-status")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Google Analytics Status", Menu = AuthorizeDefinitionConstants.Analytics)]
        public IActionResult GetGoogleAnalyticsStatus()
        {
            try
            {
                // Bu metod basitçe GA'nın yapılandırılıp yapılandırılmadığını döndürür
                // Gerçek implementasyonda GA kredensiyallerinin varlığı veya test bağlantısı yapılır
                bool isConfigured = true;
                
                return Ok(new { isConfigured });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GA durumu kontrol edilirken hata oluştu");
                return StatusCode(500, new { message = "Google Analytics durumu kontrol edilirken bir hata oluştu." });
            }
        }

        #region Helper Methods
        
        private Dictionary<string, int> MergeBrowserStats(Dictionary<string, int> stats1, Dictionary<string, int> stats2)
        {
            var result = new Dictionary<string, int>(stats1);
            
            foreach (var item in stats2)
            {
                if (result.ContainsKey(item.Key))
                {
                    result[item.Key] += item.Value;
                }
                else
                {
                    result[item.Key] = item.Value;
                }
            }
            
            // En popüler 5 tarayıcıyı döndür
            return result
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToDictionary(x => x.Key, x => x.Value);
        }
        
        private string DetermineReferrerType(string referrerDomain)
        {
            if (string.IsNullOrEmpty(referrerDomain))
                return "Doğrudan";
                
            // Arama motorları
            if (referrerDomain.Contains("google.") || 
                referrerDomain.Contains("bing.") || 
                referrerDomain.Contains("yahoo.") || 
                referrerDomain.Contains("yandex."))
                return "Arama Motoru";
                
            // Sosyal medya
            if (referrerDomain.Contains("facebook.") || 
                referrerDomain.Contains("instagram.") || 
                referrerDomain.Contains("twitter.") || 
                referrerDomain.Contains("linkedin.") ||
                referrerDomain.Contains("t.co") ||
                referrerDomain.Contains("youtube.") ||
                referrerDomain.Contains("pinterest."))
                return "Sosyal Medya";
                
            // E-posta servisleri
            if (referrerDomain.Contains("mail.") || 
                referrerDomain.Contains("gmail.") || 
                referrerDomain.Contains("outlook.") || 
                referrerDomain.Contains("yahoo."))
                return "E-posta";
                
            return "Diğer";
        }
        
        #endregion
    }
}