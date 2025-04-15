// SignalR/HubService/VisitorTrackingHubService.cs
using Application.Abstraction.Services.HubServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalR.Hubs;
using System;
using System.Threading.Tasks;

namespace SignalR.HubService
{
    public class VisitorTrackingHubService : IVisitorTrackingHubService
    {
        private readonly IHubContext<VisitorTrackingHub> _visitorTrackingHubContext;
        private readonly ILogger<VisitorTrackingHubService> _logger;

        public VisitorTrackingHubService(
            IHubContext<VisitorTrackingHub> visitorTrackingHubContext,
            ILogger<VisitorTrackingHubService> logger)
        {
            _visitorTrackingHubContext = visitorTrackingHubContext ?? throw new ArgumentNullException(nameof(visitorTrackingHubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task BroadcastVisitorStatsAsync()
        {
            try
            {
                // Bu fonksiyon hub içinde uygulandığı için burada sadece günlüğe kaydedelim
                _logger.LogInformation("BroadcastVisitorStatsAsync çağrıldı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Visitor stats yayınlanırken hata oluştu");
            }
        }

        public async Task VisitorJoinedAsync(string visitorId, string page, string ipAddress, bool isAuthenticated, string? username = null)
        {
            try
            {
                await _visitorTrackingHubContext.Clients.Group("Admins").SendAsync("VisitorJoined", new
                {
                    VisitorId = visitorId,
                    Page = page,
                    IpAddress = ipAddress,
                    IsAuthenticated = isAuthenticated,
                    Username = username ?? "Anonim",
                    JoinedAt = DateTime.UtcNow
                });
                
                _logger.LogInformation("Visitor joined message sent to admins. VisitorId: {VisitorId}", visitorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending visitor joined message for VisitorId: {VisitorId}", visitorId);
            }
        }

        public async Task VisitorLeftAsync(string visitorId)
        {
            try
            {
                await _visitorTrackingHubContext.Clients.Group("Admins").SendAsync("VisitorLeft", new
                {
                    VisitorId = visitorId,
                    LeftAt = DateTime.UtcNow
                });
                
                _logger.LogInformation("Visitor left message sent to admins. VisitorId: {VisitorId}", visitorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending visitor left message for VisitorId: {VisitorId}", visitorId);
            }
        }

        public async Task VisitorPageChangedAsync(string visitorId, string page)
        {
            try
            {
                await _visitorTrackingHubContext.Clients.Group("Admins").SendAsync("VisitorPageChanged", new
                {
                    VisitorId = visitorId,
                    Page = page,
                    ChangedAt = DateTime.UtcNow
                });
                
                _logger.LogInformation("Visitor page changed message sent to admins. VisitorId: {VisitorId}, Page: {Page}", visitorId, page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending visitor page changed message for VisitorId: {VisitorId}", visitorId);
            }
        }
    }
}