namespace Application.Models.Monitoring.Analytics;

public class PageViewSummary
{
    public string PageUrl { get; set; }
    public string PageTitle { get; set; }
    public int ViewCount { get; set; }
    public int UniqueViewerCount { get; set; }
    public double AverageTimeOnPage { get; set; }
    public double ExitRate { get; set; } // Sayfadan çıkış oranı
    
    public PageViewSummary()
    {
        PageUrl = string.Empty;
        PageTitle = string.Empty;
        ViewCount = 0;
        UniqueViewerCount = 0;
        AverageTimeOnPage = 0;
        ExitRate = 0;
    }
}