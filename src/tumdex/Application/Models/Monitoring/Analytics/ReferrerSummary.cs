namespace Application.Models.Monitoring.Analytics;

public class ReferrerSummary
{
    public string Domain { get; set; }
    public string Type { get; set; }
    public int VisitCount { get; set; }
    public int UniqueVisitorCount { get; set; }
    public double BounceRate { get; set; } // Tek sayfa ziyareti oranı
    public TimeSpan AverageVisitDuration { get; set; } 
    
    public ReferrerSummary()
    {
        Domain = string.Empty;
        Type = string.Empty;
        VisitCount = 0;
        UniqueVisitorCount = 0;
        BounceRate = 0;
        AverageVisitDuration = TimeSpan.Zero;
    }
}