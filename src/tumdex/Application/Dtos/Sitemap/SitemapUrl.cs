namespace Application.Dtos.Sitemap;

public class SitemapUrl
{
    public string Loc { get; set; }
    public DateTime? LastMod { get; set; }
    public ChangeFrequency? ChangeFreq { get; set; }
    public double? Priority { get; set; }
    public List<SitemapImage> Images { get; set; } = new();
}