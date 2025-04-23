namespace Application.Models.Monitoring.Analytics;

public class CampaignSummary
{
    public string Source { get; set; }
    public string Medium { get; set; }
    public string Campaign { get; set; }
    public int VisitCount { get; set; }
    public int ConversionCount { get; set; }
    public double ConversionRate { get; set; }
    
    public CampaignSummary()
    {
        Source = string.Empty;
        Medium = string.Empty;
        Campaign = string.Empty;
        VisitCount = 0;
        ConversionCount = 0;
        ConversionRate = 0;
    }
}