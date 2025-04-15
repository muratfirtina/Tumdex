namespace Application.Models.Monitoring;

public class VisitorSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CurrentPage { get; set; } = "/";
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public string ConnectionId { get; set; } // SignalR bağlantı kimliği
}