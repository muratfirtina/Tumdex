namespace Application.Dtos.Sitemap;

public class SitemapIssue 
{
    public SitemapIssueSeverity Severity { get; set; }
    public string Message { get; set; }
    public string? Sitemap { get; set; }
}