namespace Application.Dtos.Sitemap;

public class SitemapMonitoringReport
{
    public List<SitemapStatus> Sitemaps { get; set; } = new();
    public int TotalUrls { get; set; }
    public DateTime LastFullCheck { get; set; }
    public int HealthScore { get; set; }
    public List<SitemapIssue> Issues { get; set; } = new();
}