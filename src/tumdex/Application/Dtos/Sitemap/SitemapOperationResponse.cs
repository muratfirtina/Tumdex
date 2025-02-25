namespace Application.Dtos.Sitemap;

public class SitemapOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; } = new();
}