namespace Application.Dtos.Sitemap;

public class SitemapStatus
{
    public string Url { get; set; }
    public DateTime LastChecked { get; set; }
    public bool IsAccessible { get; set; }
    public long ResponseTime { get; set; }
    public long FileSize { get; set; }
    public int UrlCount { get; set; }
    public DateTime? LastModified { get; set; }
    public List<string> Errors { get; set; } = new();
}
