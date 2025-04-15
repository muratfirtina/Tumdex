namespace Application.Abstraction.Services.HubServices;

public interface IVisitorTrackingHubService
{
    Task VisitorJoinedAsync(string visitorId, string page, string ipAddress, bool isAuthenticated, string? username = null);
    Task VisitorLeftAsync(string visitorId);
    Task VisitorPageChangedAsync(string visitorId, string page);
    Task BroadcastVisitorStatsAsync();
}